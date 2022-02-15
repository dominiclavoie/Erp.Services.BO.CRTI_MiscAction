using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Erp.Tables;
using Erp.Tablesets;
using Erp.Services.BO;
using Epicor.Data;
using Ice;
using Ice.Tables;
using Ice.Tablesets;

namespace Erp.BO.CRTI_MiscAction
{
    class JobBatchProductionVRODDroite : BaseJobBatchProduction, IJobBatchProduction
    {
        private static Func<ErpContext, string, string[], IEnumerable<LaborDtl>> selectLaborByJobBatch;
        public JobBatchProductionVRODDroite(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session){}
        private IEnumerable<LaborDtl> SelectLaborByJobBatch(string company, List<string> sysRowIDs)
        {
            if (selectLaborByJobBatch == null)
            {
                selectLaborByJobBatch = DBExpressionCompiler.Compile<ErpContext, string, string[], LaborDtl>(
                    (Expression<Func<ErpContext, string, string[], IEnumerable<LaborDtl>>>)(
                        (ErpContext context, string company_ex, string[] sysRowIDs_ex) => 
                            context.LaborDtl.Where((LaborDtl row) => 
                                row.Company == company_ex &&
                                sysRowIDs_ex.Contains(row["UD_RefBatchSubmission_c"].ToString())
                            )
                        ),(Epicor.Data.Cache)0, true);
            }
            return selectLaborByJobBatch(Db, company, sysRowIDs.ToArray());
        }
        private void SetJobNum(UD105A prod, string resID)
        {
            var barlist = (from ud in this.Db.UD104
                           where ud.Company == this.Session.CompanyID &&
                                 ud.Key2 == "JobOper" &&
                                 ud.ShortChar01 == prod.ChildKey3
                           select ud).FirstOrDefault();
            if( barlist != null)
            {
                var prd = (from ud in this.Db.UD105A
                           where ud.Company == prod.Company &&
                                 ud.Key1 == prod.Key1 &&
                                 ud.Key2 == prod.Key2 &&
                                 ud.Key3 == prod.Key3 &&
                                 ud.Key4 == prod.Key4 &&
                                 ud.Key5 == prod.Key5 &&
                                 ud.ChildKey1 == prod.ChildKey1 &&
                                 ud.ChildKey2 == prod.ChildKey2 &&
                                 ud.ChildKey3 == prod.ChildKey3 &&
                                 ud.ChildKey4 == prod.ChildKey4 &&
                                 ud.ChildKey5 == prod.ChildKey5
                           select ud).FirstOrDefault();
                if (prd != null)
                {
                    using (var txScope = IceContext.CreateDefaultTransactionScope())
                    {
                        prd.Character06 = resID;
                        prd.Character07 = barlist.Key3;
                        this.Db.Validate();
                        txScope.Complete();
                    }
                }
            }
        }
        public void Distribute(DataSet dsJobBatch, ref DataSet dsMiscAction)
        {
            if( !dsJobBatch.Tables.Contains("UD105") || dsJobBatch.Tables["UD105"].Rows.Count == 0)
            {
                throw new BLException("Les informations du JobBatch sont manquantes.");
            }
            dsMiscAction.Tables["UD105A"].Rows.Clear();
            foreach(DataRow headRow in dsJobBatch.Tables["UD105"].Rows)
            {
                var groups = from row in SelectProduction(this.Session.CompanyID, headRow["Key1"].ToString(), headRow["Key2"].ToString(), headRow["Key3"].ToString(), headRow["Key4"].ToString(), headRow["Key5"].ToString())
                             group row by new
                             {
                                 ResourceID = row.Character06,
                                 JobNum = row.Character07,
                                 OpCode = row.Character09
                             } into grp
                             select new
                             {
                                 grp.Key.ResourceID,
                                 grp.Key.JobNum,
                                 grp.Key.OpCode,
                                 QteProd = grp.Sum(r => r.Number01 * r.Number05)
                             };
                DataTable dtTempsRepartir = dsMiscAction.Tables["TempsRepartir"];
                foreach (UD105A prod in SelectProduction(this.Session.CompanyID, headRow["Key1"].ToString(), headRow["Key2"].ToString(), headRow["Key3"].ToString(), headRow["Key4"].ToString(), headRow["Key5"].ToString()))
                {
                    DataRow[] rowTemps = dtTempsRepartir.Select(string.Format("ResourceID = '{0}' AND JobNum = '{1}' AND OpCode = '{2}'", prod.Character06, prod.Character07, prod.Character09));
                    if( rowTemps.Length == 0)
                    {
                        SetJobNum(prod, headRow["Character02"].ToString());
                        throw new BLException(string.Format("Aucun temps à répartir pour la ligne produite #{0}", prod.ChildKey3));
                    }
                    decimal MOD = decimal.Parse(rowTemps[0]["MOD"].ToString());
                    decimal FGF = decimal.Parse(rowTemps[0]["FGF"].ToString());
                    var group = (from gr in groups
                                where gr.ResourceID == prod.Character06 &&
                                      gr.JobNum == prod.Character07 &&
                                      gr.OpCode == prod.Character09
                                select gr).FirstOrDefault();
                    if( group == null )
                    {
                        throw new BLException("Regroupement de production manquant.");
                    }
                    decimal totalProd = group.QteProd;
                    decimal lineProd = prod.Number01 * prod.Number05;
                    DataRow newRow = dsMiscAction.Tables["UD105A"].NewRow();
                    Copy<UD105A>(newRow, prod, "Number06", "Number07", "SysRevID");
                    newRow["Number06"] = totalProd == 0m ? 0m : MOD * lineProd / totalProd;
                    newRow["Number07"] = totalProd == 0m ? 0m : FGF * lineProd / totalProd;
                    dsMiscAction.Tables["UD105A"].Rows.Add(newRow);
                }
                dsMiscAction.AcceptChanges();
            }
        }

        public void SubmitLabor(DataSet dsJobBatch, DataSet dsMiscAction, Erp.BO.CRTI_MiscAction.OperationInteraction operationInteraction)
        {
            if (!dsMiscAction.Tables.Contains("ValidationJobBatch"))
            {
                return;
            }
            Erp.Contracts.LaborSvcContract svcLabor = Ice.Assemblies.ServiceRenderer.GetService<Erp.Contracts.LaborSvcContract>(Db);
            GetJobOperDetails(GetJobNums(dsMiscAction));
            try
            {
                string employeeNum = "";
                int hedSeq = -1;
                DateTime clockDate = DateTime.Today;
                var laborGrps =  from row in dsMiscAction.Tables["ValidationJobBatch"].AsEnumerable()
                                 group row by new
                                 {
                                     EmployeeNum = row.Field<string>("EmployeeNum"),
                                     ResourceID = row.Field<string>("ResourceID"),
                                     JobNum = row.Field<string>("JobNum"),
                                     OpCode = row.Field<string>("OpCode"),
                                     ClockDate = row.Field<string>("ClockDate")
                                 } into grp
                                 select new
                                 {
                                     grp.Key.EmployeeNum,
                                     grp.Key.ResourceID,
                                     grp.Key.JobNum,
                                     grp.Key.OpCode,
                                     grp.Key.ClockDate,
                                     MOD = grp.Sum(r => r.Field<Decimal>("MOD")),
                                     FGF = grp.Sum(r => r.Field<Decimal>("FGF"))
                                 };
                foreach (var laborGrp in laborGrps)
                {
                    bool isSetup = operationInteraction.OperationIs("IsSetup", laborGrp.OpCode);
                    string opCode = laborGrp.OpCode;
                    string opRef;
                    if(isSetup && operationInteraction.GetOperationSetupRef(laborGrp.OpCode, out opRef))
                    {
                        opCode = opRef;
                    }
                    var labor = (from row in dsJobBatch.Tables["LaborDtl"].AsEnumerable()
                                 where row.Field<string>("EmployeeNum") == laborGrp.EmployeeNum &&
                                       row.Field<string>("ResourceID") == laborGrp.ResourceID &&
                                       row.Field<string>("OpCode") == laborGrp.OpCode
                                 select row).FirstOrDefault();
                    employeeNum = laborGrp.EmployeeNum;
                    hedSeq = int.Parse(labor["LaborHedSeq"].ToString());
                    clockDate = DateTime.Parse(laborGrp.ClockDate);
                    string[] refJobBatch = labor["UD_RefJobBatch_c"].ToString().Split('-');
                    string batchSysRowID = "";
                    if(refJobBatch.Length == 2)
                    {
                        var jbBatch = (from row in dsJobBatch.Tables["UD105"].AsEnumerable()
                                       where row.Field<string>("Key1") == refJobBatch[0] &&
                                             row.Field<string>("Key2") == refJobBatch[1]
                                       select row).FirstOrDefault();
                        if(jbBatch != null)
                        {
                            batchSysRowID = jbBatch["SysRowID"].ToString();
                        }
                    }
                    CreateLaborDtl(svcLabor, batchSysRowID, hedSeq, clockDate, laborGrp.JobNum, laborGrp.ResourceID, opCode, isSetup, laborGrp.MOD, laborGrp.FGF);
                }
                foreach (DataRow headRow in dsJobBatch.Tables["UD105"].Rows)
                {
                    var groups = from row in SelectProduction(this.Session.CompanyID, headRow["Key1"].ToString(), headRow["Key2"].ToString(), headRow["Key3"].ToString(), headRow["Key4"].ToString(), headRow["Key5"].ToString())
                                 group row by new
                                 {
                                     ResourceID = row.Character06,
                                     JobNum = row.Character07,
                                     OpCode = row.Character09
                                 } into grp
                                 select new
                                 {
                                     grp.Key.ResourceID,
                                     grp.Key.JobNum,
                                     grp.Key.OpCode,
                                     QteProd = grp.Sum(r => r.Number01 * r.Number05),
                                     Lines = grp
                                 };
                    foreach(var group in groups)
                    {
                        string batchSysRowID = "";
                        string resID = group.ResourceID;
                        var jbBatch = (from row in dsJobBatch.Tables["UD105"].AsEnumerable()
                                       where row.Field<string>("Character02") == resID
                                       select row).FirstOrDefault();
                        if (jbBatch != null)
                        {
                            batchSysRowID = jbBatch["SysRowID"].ToString();
                        }
                        CreateLaborDtl(svcLabor, batchSysRowID, hedSeq, clockDate, group.JobNum, group.ResourceID, group.OpCode, false, 0m, 0m, group.QteProd);
                    }
                }
            }
            catch(Exception ex)
            {
                throw new BLException(ex.Message);
            }
            finally
            {
                svcLabor.Dispose();
                svcLabor = null;
                ClearLists();
            }
        }

        public void RecallLabor(DataSet dsJobBatch)
        {

        }

        private string CreateLaborDtl(Erp.Contracts.LaborSvcContract svcLabor, string batchSysRowID, int hedSeq, DateTime clockDate, string jobNum, string resID, string opCode, bool isSetup, decimal tempsAttribueEmployeJob, decimal hrsFGF, decimal prodQty = 0m)
        {
            string sysRowID = string.Empty;
            int assSeq = 0;
            int oprSeq;
            bool flag = GetJobOperSeq(jobNum, opCode, out oprSeq);
            if (!flag)
            {
                throw new BLException(string.Format("Dans le bon de travail #{0}, l'opération {1} est invalide.", jobNum, opCode));
            }
            LaborTableset dsLabor = svcLabor.GetByID(hedSeq);
            decimal tempsEmploye = tempsAttribueEmployeJob == 0m ? 0.01m : tempsAttribueEmployeJob;
            svcLabor.GetNewLaborDtlWithHdr(ref dsLabor, clockDate, 0m, clockDate, tempsEmploye, hedSeq);
            if (isSetup)
            {
                svcLabor.DefaultLaborType(ref dsLabor, "S");
                svcLabor.ChangeLaborType(ref dsLabor);
            }
            LaborDtlRow newRow = dsLabor.LaborDtl.Where(tt => tt.Added()).FirstOrDefault();
            if (tempsAttribueEmployeJob == 0m)
            {
                newRow.LaborHrs = 0m;
            }
            newRow.BurdenHrs = hrsFGF;
            svcLabor.DefaultJobNum(ref dsLabor, jobNum);
            svcLabor.DefaultAssemblySeq(ref dsLabor, assSeq);
            string vMessage = string.Empty;
            svcLabor.DefaultOprSeq(ref dsLabor, oprSeq, out vMessage);
            if (!string.IsNullOrEmpty(resID))
            {
                svcLabor.CheckResourceGroup(ref dsLabor, resID, out vMessage);
                svcLabor.OverridesResource(ref dsLabor, resID);
            }
            svcLabor.LaborRateCalc(ref dsLabor);
            if (prodQty != 0m)
            {
                svcLabor.DefaultLaborQty(ref dsLabor, prodQty, out vMessage);
                newRow.LaborQty = prodQty;
            }
            svcLabor.CheckWarnings(ref dsLabor, out vMessage);
            newRow["UD_RefBatchSubmission_c"] = batchSysRowID;
            svcLabor.Update(ref dsLabor);
            newRow.RowMod = "U";
            svcLabor.SubmitForApproval(ref dsLabor, false, out vMessage);
            sysRowID = newRow.SysRowID.ToString();
            return sysRowID;
        }
       
    }
}

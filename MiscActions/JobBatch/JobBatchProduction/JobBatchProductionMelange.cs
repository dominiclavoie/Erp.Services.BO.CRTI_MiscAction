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
    class JobBatchProductionMelange : BaseJobBatchProduction, IJobBatchProduction
    {
        public JobBatchProductionMelange(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session){}
        private List<UD105A> CombinerEnregistrementMelange(DataSet dsJobBatch)
        {
            List<UD105A> prods = new List<UD105A>();
            foreach (DataRow headRow in dsJobBatch.Tables["UD105"].Rows)
            {
                prods.AddRange(SelectProduction(this.Session.CompanyID, headRow["Key1"].ToString(), headRow["Key2"].ToString(), headRow["Key3"].ToString(), headRow["Key4"].ToString(), headRow["Key5"].ToString()));
            }
            return prods;
        }

        protected override List<string> GetJobNums(DataSet dsJobBatch)
        {
            List<string> jobNums = new List<string>();
            if (!dsJobBatch.Tables.Contains("UD105"))
            {
                return jobNums;
            }
            foreach (DataRow headRow in dsJobBatch.Tables["UD105"].Rows)
            {
                List<string> jNums = (from row in SelectProduction(this.Session.CompanyID, headRow["Key1"].ToString(), headRow["Key2"].ToString(), headRow["Key3"].ToString(), headRow["Key4"].ToString(), headRow["Key5"].ToString())
                                      group row by new { JobNum = row.ShortChar01 } into grp
                                      select new { grp.Key.JobNum }).Select(tt => tt.JobNum).ToList();
                if (jNums.Any())
                {
                    jobNums.AddRange(jNums);
                }
            }
            return jobNums;
        }

        private DataTable SelectMiscActionProduction(DataSet dsMiscAction, string company, string key1, string key2, string key3, string key4, string key5)
        {
            if (!dsMiscAction.Tables.Contains("UD105A"))
            {
                return new DataTable("UD105A");
            }
            return (from row in dsMiscAction.Tables["UD105A"].AsEnumerable()
                    where row.Field<string>("Company") == company &&
                          row.Field<string>("Key1") == key1 &&
                          row.Field<string>("Key2") == key2 &&
                          row.Field<string>("Key3") == key3 &&
                          row.Field<string>("Key4") == key4 &&
                          row.Field<string>("Key5") == key5
                    select row).CopyToDataTable();
        }

        public void Distribute(DataSet dsJobBatch, ref DataSet dsMiscAction)
        {
            if( !dsJobBatch.Tables.Contains("UD105") || dsJobBatch.Tables["UD105"].Rows.Count == 0)
            {
                throw new BLException("Les informations du JobBatch sont manquantes.");
            }
            dsMiscAction.Tables["UD105A"].Rows.Clear();
            DataTable dtTempsRepartir = dsMiscAction.Tables["TempsRepartir"];
            List<UD105A> prods = CombinerEnregistrementMelange(dsJobBatch);
            int nb = prods.Count;
            foreach(UD105A prod in prods)
            {
                DataRow[] rowTemps = dtTempsRepartir.Select(string.Format("ResourceID = '{0}' AND OpCode = 'MELANGE'", prod.Character06));
                if (rowTemps.Length == 0)
                {
                    throw new BLException("Aucun temps à répartir pour l'opération Mélange");
                }
                decimal MOD = decimal.Parse(rowTemps[0]["MOD"].ToString());
                decimal FGF = decimal.Parse(rowTemps[0]["FGF"].ToString());
                DataRow newRow = dsMiscAction.Tables["UD105A"].NewRow();
                Copy<UD105A>(newRow, prod, "Number06", "Number07", "SysRevID");
                newRow["Number06"] = nb == 0 ? 0m : MOD / (decimal)nb;
                newRow["Number07"] = nb == 0 ? 0m : FGF / (decimal)nb;
                dsMiscAction.Tables["UD105A"].Rows.Add(newRow);
            }
            dsMiscAction.AcceptChanges();
        }

        public void SubmitLabor(DataSet dsJobBatch, DataSet dsMiscAction, Erp.BO.CRTI_MiscAction.OperationInteraction operationInteraction)
        {
            if (!dsMiscAction.Tables.Contains("ValidationJobBatch"))
            {
                return;
            }
            Erp.Contracts.LaborSvcContract svcLabor = Ice.Assemblies.ServiceRenderer.GetService<Erp.Contracts.LaborSvcContract>(Db);
            GetJobOperDetails(GetJobNums(dsJobBatch));
            try
            {
                ReceiptFromMfgController receiptFromMfgController = new ReceiptFromMfgController(this.Db, this.Session);
                string employeeNum = "";
                int hedSeq = -1;
                DateTime clockDate = DateTime.Today;
                var laborTtls = from row in dsMiscAction.Tables["ValidationJobBatch"].AsEnumerable()
                                group row by new
                                {
                                    ResourceID = row.Field<string>("ResourceID"),
                                    OpCode = row.Field<string>("OpCode"),
                                    ClockDate = row.Field<string>("ClockDate")
                                } into grp
                                select new
                                {
                                    grp.Key.ResourceID,
                                    grp.Key.OpCode,
                                    grp.Key.ClockDate,
                                    MOD = grp.Sum(r => r.Field<Decimal>("MOD")),
                                    FGF = grp.Sum(r => r.Field<Decimal>("FGF"))
                                };
                var laborGrps =  from row in dsMiscAction.Tables["ValidationJobBatch"].AsEnumerable()
                                 group row by new
                                 {
                                     EmployeeNum = row.Field<string>("EmployeeNum"),
                                     ResourceID = row.Field<string>("ResourceID"),
                                     OpCode = row.Field<string>("OpCode"),
                                     ClockDate = row.Field<string>("ClockDate")
                                 } into grp
                                 select new
                                 {
                                     grp.Key.EmployeeNum,
                                     grp.Key.ResourceID,
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
                    var laborTotal = laborTtls.Where(tt => tt.ResourceID == laborGrp.ResourceID &&
                                                           tt.OpCode == laborGrp.OpCode &&
                                                           tt.ClockDate == laborGrp.ClockDate).FirstOrDefault();
                    decimal totalMOD = laborTotal.MOD;
                    decimal totalFGF = laborTotal.FGF;
                    var labor = (from row in dsJobBatch.Tables["LaborDtl"].AsEnumerable()
                                 where row.Field<string>("EmployeeNum") == laborGrp.EmployeeNum &&
                                       row.Field<string>("ResourceID") == laborGrp.ResourceID &&
                                       row.Field<string>("OpCode") == laborGrp.OpCode
                                 select row).FirstOrDefault();
                    employeeNum = laborGrp.EmployeeNum;
                    hedSeq = int.Parse(labor["LaborHedSeq"].ToString());
                    clockDate = DateTime.Parse(laborGrp.ClockDate);
                    foreach (DataRow headRow in dsJobBatch.Tables["UD105"].Rows)
                    {
                        var groups = from row in SelectMiscActionProduction(dsMiscAction, this.Session.CompanyID, headRow["Key1"].ToString(), headRow["Key2"].ToString(), headRow["Key3"].ToString(), headRow["Key4"].ToString(), headRow["Key5"].ToString()).AsEnumerable()
                                     group row by new
                                     {
                                         ResourceID = row.Field<string>("Character06"),
                                         JobNum = row.Field<string>("ShortChar01"),
                                         OpCode = "MELANGE"
                                     } into grp
                                     select new
                                     {
                                         grp.Key.ResourceID,
                                         grp.Key.JobNum,
                                         grp.Key.OpCode,
                                         MOD = grp.Sum(r => r.Field<Decimal>("Number06")),
                                         FGF = grp.Sum(r => r.Field<Decimal>("Number07")),
                                         Lines = grp
                                     };
                        foreach(var group in groups)
                        {
                            string batchSysRowID = headRow["SysRowID"].ToString();
                            string resID = group.ResourceID;
                            decimal mod = group.MOD * laborGrp.MOD / totalMOD;
                            decimal fgf = group.FGF * laborGrp.FGF / totalFGF;
                            CreateLaborDtl(svcLabor, batchSysRowID, hedSeq, clockDate, group.JobNum, group.ResourceID, group.OpCode, false, mod, fgf);
                        }
                    }
                }
                foreach (DataRow headRow in dsJobBatch.Tables["UD105"].Rows)
                {
                    var groups = from row in SelectMiscActionProduction(dsMiscAction, this.Session.CompanyID, headRow["Key1"].ToString(), headRow["Key2"].ToString(), headRow["Key3"].ToString(), headRow["Key4"].ToString(), headRow["Key5"].ToString()).AsEnumerable()
                                 group row by new
                                 {
                                     ResourceID = row.Field<string>("Character06"),
                                     JobNum = row.Field<string>("ShortChar01"),
                                     OpCode = "MELANGE"
                                 } into grp
                                 select new
                                 {
                                     grp.Key.ResourceID,
                                     grp.Key.JobNum,
                                     grp.Key.OpCode,
                                     QteProd = grp.Sum(r => r.Field<Decimal>("Number05")),
                                     Lines = grp
                                 };
                    foreach (var group in groups)
                    {
                        string batchSysRowID = headRow["SysRowID"].ToString();
                        string resID = group.ResourceID;
                        CreateLaborDtl(svcLabor, batchSysRowID, hedSeq, clockDate, group.JobNum, group.ResourceID, group.OpCode, false, 0m, 0m, group.QteProd);
                        foreach(DataRow lineRow in group.Lines)
                        {
                            string jobNum = lineRow["ShortChar01"].ToString();
                            decimal prodQty = decimal.Parse(lineRow["Number05"].ToString());
                            string lotNum = lineRow["ShortChar08"].ToString();
                            string jobNum2 = lineRow["ShortChar05"].ToString();
                            int assm2 = int.Parse(lineRow["ShortChar06"].ToString());
                            int jobSeq2 = int.Parse(lineRow["ShortChar07"].ToString());
                            receiptFromMfgController.ReceiveToJob(jobNum, 0, lotNum, prodQty, jobNum2, assm2, jobSeq2);
                        }
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

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
using Extension_MiscAction;

namespace Erp.BO.CRTI_MiscAction
{
    class JobBatchProductionUsinage : BaseJobBatchProduction, IJobBatchProduction
    {
        private static Func<ErpContext, string, string[], IEnumerable<LaborDtl>> selectLaborByJobBatch;
        public JobBatchProductionUsinage(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session){}
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
                foreach (UD105A prod in SelectProduction(this.Session.CompanyID, headRow["Key1"].ToString(), headRow["Key2"].ToString(), headRow["Key3"].ToString(), headRow["Key4"].ToString(), headRow["Key5"].ToString()))
                {
                    DataRow newRow = dsMiscAction.Tables["UD105A"].NewRow();
                    Copy<UD105A>(newRow, prod, "Number06", "Number07", "SysRevID");
                    dsMiscAction.Tables["UD105A"].Rows.Add(newRow);
                }
                foreach (UD105A prod in SelectRebutProduction(this.Session.CompanyID, headRow["Key1"].ToString(), headRow["Key2"].ToString(), headRow["Key3"].ToString(), headRow["Key4"].ToString(), headRow["Key5"].ToString()))
                {
                    DataRow newRow = dsMiscAction.Tables["UD105A"].NewRow();
                    Copy<UD105A>(newRow, prod, "Number06", "Number07", "SysRevID");
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
            //ReceiptFromMfgController receiptFromMfgController = new ReceiptFromMfgController(this.Db, this.Session);
            List<string> jobNums = GetJobNums(dsMiscAction);
            GetJobOperDetails(jobNums);
            GetLastJobOper(jobNums);
            string currentLaborEntry = string.Empty;
            try
            {
                foreach (DataRow laborRow in dsMiscAction.Tables["ValidationJobBatch"].Rows)
                {
                    currentLaborEntry = string.Format("{0}-{1}", laborRow["LaborHedSeq"].ToString(), laborRow["LaborDtlSeq"].ToString());
                    int hSeq = int.Parse(laborRow["LaborHedSeq"].ToString());
                    int dSeq = int.Parse(laborRow["LaborDtlSeq"].ToString());
                    decimal laborHours = decimal.Parse(laborRow["Heures"].ToString());
                    decimal burdenHours = decimal.Parse(laborRow["FGF"].ToString());
                    SubmitSplitTime(svcLabor, dsJobBatch, hSeq, dSeq, laborHours, burdenHours);
                }

                foreach (DataRow headRow in dsJobBatch.Tables["UD105"].Rows)
                {
                    var groups = from row in SelectProduction(this.Session.CompanyID, headRow["Key1"].ToString(), headRow["Key2"].ToString(), headRow["Key3"].ToString(), headRow["Key4"].ToString(), headRow["Key5"].ToString())
                                 group row by new
                                 {
                                     ResourceID = row.Character06,
                                     JobNum = row.Character07,
                                     OpCode = row.Character09,
                                     IDLigneProd = row.ChildKey3
                                 } into grp
                                 select new
                                 {
                                     grp.Key.ResourceID,
                                     grp.Key.JobNum,
                                     grp.Key.OpCode,
                                     grp.Key.IDLigneProd,
                                     QteProd = grp.Sum(r => r.Number05),
                                     Lines = grp
                                 };
                    foreach (var group in groups)
                    {
                        currentLaborEntry = string.Format("Ligne prod:{0}", group.IDLigneProd);
                        string batchSysRowID = headRow["SysRowID"].ToString();
                        string resID = group.ResourceID;
                        if(!GetEmployeeForProd(dsMiscAction, resID, group.IDLigneProd, out int hedSeq, out DateTime clockDate))
                        {
                            throw new BLException(string.Format("Aucun temps de travail associé à l'ID {0} produit sur {1}", group.IDLigneProd, resID));
                        }
                        decimal prodQty = Math.Round((group.QteProd), 4, MidpointRounding.AwayFromZero);
                        CreateLaborDtl(svcLabor, batchSysRowID, hedSeq, clockDate, group.JobNum, group.ResourceID, group.OpCode, false, 0m, 0m, prodQty);
                    }
                
                    var scraps = from row in SelectRebutProduction(this.Session.CompanyID, headRow["Key1"].ToString(), headRow["Key2"].ToString(), headRow["Key3"].ToString(), headRow["Key4"].ToString(), headRow["Key5"].ToString())
                                 group row by new
                                 {
                                     ResourceID = row.Character06,
                                     JobNum = row.Character07,
                                     OpCode = row.Character09,
                                     IDLigneProd = row.ChildKey3,
                                     ReasonCode = row.ShortChar03
                                 } into grp
                                 select new
                                 {
                                     grp.Key.ResourceID,
                                     grp.Key.JobNum,
                                     grp.Key.OpCode,
                                     grp.Key.IDLigneProd,
                                     grp.Key.ReasonCode,
                                     QteScrap = grp.Sum(r => r.Number05),
                                     Lines = grp
                                 };
                    foreach (var scrap in scraps)
                    {
                        currentLaborEntry = string.Format("Ligne scrap:{0}", scrap.IDLigneProd);
                        string batchSysRowID = headRow["SysRowID"].ToString();
                        if(!GetEmployeeForProd(dsMiscAction, scrap.ResourceID, scrap.IDLigneProd, out int hedSeq, out DateTime clockDate))
                        {
                            throw new BLException(string.Format("Aucun temps de travail associé à l'ID {0} produit sur {1}", scrap.IDLigneProd, scrap.ResourceID));
                        }
                        decimal scrapQty = Math.Round((scrap.QteScrap), 4, MidpointRounding.AwayFromZero);
                        CreateScrapLaborDtl(svcLabor, batchSysRowID, hedSeq, clockDate, scrap.JobNum, scrap.ResourceID, scrap.OpCode, scrapQty, scrap.ReasonCode);
                    }
                }
                foreach (DataRow headRow in dsJobBatch.Tables["UD105"].Rows)
                {
                    UD105 header = SelectHeader(this.Session.CompanyID, headRow["Key1"].ToString(), headRow["Key2"].ToString(), headRow["Key3"].ToString(), headRow["Key4"].ToString(), headRow["Key5"].ToString()).FirstOrDefault();
                    if (header != null)
                    {
                        using (var txScope = IceContext.CreateDefaultTransactionScope())
                        {
                            header.CheckBox01 = true;
                            this.Db.Validate<UD105>(header);
                            txScope.Complete();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new BLException(ex.Message + "Info ligne erreur => " + currentLaborEntry);
            }
            finally
            {
                svcLabor.Dispose();
                svcLabor = null;
                //receiptFromMfgController.Dispose();
                //receiptFromMfgController = null;
                ClearLists();
            }
        }

        public void RecallLabor(DataSet dsJobBatch)
        {

        }

        public bool GetEmployeeForProd(DataSet dsMiscAction, string resID, string idLigneProd, out int hedSeq, out DateTime clockDate)
        {
            hedSeq = -1;
            clockDate = DateTime.Now;
            var labor = (from lb in (
                            from row in dsMiscAction.Tables["ValidationJobBatch"].AsEnumerable()
                            group row by new
                            {
                                EmployeeNum = row.Field<string>("EmployeeNum"),
                                LaborHedSeq = row.Field<string>("LaborHedSeq"),
                                ResourceID = row.Field<string>("ResourceID"),
                                IDLigneProd = row.Field<string>("IDLigneProduction"),
                                LaborType = row.Field<string>("LaborType"),
                                ClockDate = row.Field<string>("ClockDate")
                            } into grp
                            select new
                            {
                                grp.Key.EmployeeNum,
                                grp.Key.LaborHedSeq,
                                grp.Key.ResourceID,
                                grp.Key.IDLigneProd,
                                grp.Key.LaborType,
                                grp.Key.ClockDate,
                                MOD = grp.Sum(r => r.Field<Decimal>("Heures"))
                            })
                         where lb.ResourceID == resID &&
                               lb.IDLigneProd == idLigneProd &&
                               lb.LaborType == "P"
                         orderby lb.MOD descending
                         select lb).FirstOrDefault();
            if(labor == null)
            {
                return false;
            }
            hedSeq = int.Parse(labor.LaborHedSeq);
            clockDate = DateTime.Parse(labor.ClockDate);
            return true;
        }

        private string GetRefBatchSoumission(DataSet dsJobBatch, string refBatch)
        {
            string[] refs = refBatch.Split('-');
            if(refs.Length != 2)
            {
                return string.Empty;
            }
            var header = (from ud in dsJobBatch.Tables["UD105"].AsEnumerable()
                          where ud.Field<string>("Key1") == refs[0] &&
                                ud.Field<string>("Key2") == refs[1]
                          select new
                          {
                              RefBatchSoumission = ud.Field<Guid>("SysRowID").ToString()
                          }).FirstOrDefault();
            return header == null ? string.Empty : header.RefBatchSoumission;
        }

        private void SubmitSplitTime(Erp.Contracts.LaborSvcContract svcLabor, DataSet dsJobBatch, int hedSeq, int dtlSeq, decimal laborHours, decimal burdenHours)
        {
            string whereClauseLaborHed = string.Format("LaborHedSeq = {0}", hedSeq);
            string whereClauseLaborDtl = string.Format("LaborHedSeq = {0} AND LaborDtlSeq = {1}", hedSeq, dtlSeq);
            LaborTableset dsLabor = svcLabor.GetRows(whereClauseLaborHed, whereClauseLaborDtl, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, 0, out bool morePages);
            LaborDtlRow ttLaborDtl = dsLabor.LaborDtl.Where(tt => tt.LaborHedSeq == hedSeq && tt.LaborDtlSeq == dtlSeq).FirstOrDefault();
            if(ttLaborDtl == null)
            {
                throw new BLException("L'entrée de temps est introuvable");
            }
            if(ttLaborDtl.TimeStatus == "A")
            {
                throw new BLException("L'entrée de temps a déjà été approuvée");
            }
            string refBatchSoumission = GetRefBatchSoumission(dsJobBatch, ttLaborDtl.UDField<string>("UD_RefJobBatch_c"));
            ttLaborDtl.RowMod = "U";
            ttLaborDtl.LaborHrs = laborHours;
            ttLaborDtl.BurdenHrs = burdenHours;
            ttLaborDtl.SetUDField("UD_RefBatchSubmission_c", refBatchSoumission);
            svcLabor.CheckWarnings(ref dsLabor, out string vMessage);
            svcLabor.Update(ref dsLabor);
            ttLaborDtl.RowMod = "U";
            svcLabor.SubmitForApproval(ref dsLabor, false, out string vMessage2);
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
            if(newRow == null)
            {
                throw new BLException("La validation a échoué.");
            }
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
            newRow.SetUDField("UD_RefBatchSubmission_c", batchSysRowID);
            svcLabor.Update(ref dsLabor);
            newRow.RowMod = "U";
            svcLabor.SubmitForApproval(ref dsLabor, false, out vMessage);
            sysRowID = newRow.SysRowID.ToString();
            return sysRowID;
        }
        private string CreateScrapLaborDtl(Erp.Contracts.LaborSvcContract svcLabor, string batchSysRowID, int hedSeq, DateTime clockDate, string jobNum, string resID, string opCode, decimal scrapQty, string scrapReasonCode)
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
            svcLabor.GetNewLaborDtlWithHdr(ref dsLabor, clockDate, 0m, clockDate, 1m, hedSeq);
            LaborDtlRow newRow = dsLabor.LaborDtl.Where(tt => tt.Added()).FirstOrDefault();
            if (newRow == null)
            {
                throw new BLException("La validation a échoué.");
            }
            newRow.LaborHrs = 0m;
            newRow.BurdenHrs = 0m;
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
            svcLabor.VerifyScrapQty(ref dsLabor, scrapQty, out vMessage);
            newRow.ScrapQty = scrapQty;
            newRow.ScrapReasonCode = scrapReasonCode;
            svcLabor.CheckWarnings(ref dsLabor, out vMessage);
            newRow.SetUDField("UD_RefBatchSubmission_c", batchSysRowID);
            svcLabor.Update(ref dsLabor);
            newRow.RowMod = "U";
            svcLabor.SubmitForApproval(ref dsLabor, false, out vMessage);
            sysRowID = newRow.SysRowID.ToString();
            return sysRowID;
        }

    }
}

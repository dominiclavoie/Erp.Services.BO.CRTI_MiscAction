using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
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
    public enum JobBatchType
    {
        VRODDroite,
        ProfileBrut,
        Volvo,
        Melange,
        Usinage,
        Assemblage
    }
    class ValidationJobBatch : BaseMiscAction, IMiscAction
    {
        private Erp.BO.CRTI_MiscAction.OperationInteraction operationInteraction;
        private JobBatchType jobBatchType;
        private IJobBatchProduction jobBatchProduction;
        private IJobBatchMOD jobBatchMOD;
        public ValidationJobBatch(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session)
        {
            operationInteraction = new Erp.BO.CRTI_MiscAction.OperationInteraction(db, session);
        }

        #region Private Members

        
        protected override void DataSetLoaded()
        {
            if (this.dsMiscAction.Tables.Contains("JobBatch") && this.dsMiscAction.Tables["JobBatch"].Rows.Count > 0)
            {
                string jbType = this.dsMiscAction.Tables["JobBatch"].Rows[0]["Type"].ToString();
                LoadJobBatchType(jbType);
            }
            else
            {
                throw new BLException("Des données du système sont manquantes.");
            }
        }
        public override DataTable[] GetDataTable()
        {
            List<DataTable> dts = new List<DataTable>();
            DataTable dt = new DataTable("JobBatch");
            dt.Locale = CultureInfo.InvariantCulture;
            dt.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("Type", typeof(string))
            });
            dt.Rows.Add(this.jobBatchType.ToString());
            dts.Add(dt);
            DataTable dt2 = new DataTable("WeekJobBatchs");
            dt2.Locale = CultureInfo.InvariantCulture;
            dt2.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("Date", typeof(DateTime)),
                new DataColumn("Shift", typeof(string)),
                new DataColumn("Validé", typeof(bool)),
                new DataColumn("Soumis", typeof(bool))
            });
            dts.Add(dt2);
            dts.Add(new Ice.BO.UD105DataSet.UD105ADataTable().Clone());
            DataTable[] modDts = this.jobBatchMOD.GetDataTable();
            if(modDts != null)
            {
                dts.AddRange(modDts);
            }
            return dts.ToArray();
        }

        #endregion
        public void LoadJobBatchType(string _jobBatchType)
        {
            try
            {
                this.jobBatchType = (JobBatchType)Enum.Parse(typeof(JobBatchType), _jobBatchType);
            }
            catch (Exception)
            {
                throw new BLException("Le type de JobBatch est invalide.");
            }
            switch (this.jobBatchType)
            {
                case JobBatchType.VRODDroite:
                    this.jobBatchProduction = new JobBatchProductionVRODDroite(this.Db, this.Session);
                    this.jobBatchMOD = new JobBatchMODVRODDroite(this.Db, this.Session);
                    break;

                case JobBatchType.ProfileBrut:
                    this.jobBatchProduction = new JobBatchProductionProfileBrut(this.Db, this.Session);
                    this.jobBatchMOD = new JobBatchMODProfileBrut(this.Db, this.Session);
                    break;

                case JobBatchType.Volvo:
                case JobBatchType.Usinage:
                    this.jobBatchProduction = new JobBatchProductionUsinage(this.Db, this.Session);
                    this.jobBatchMOD = new JobBatchMODUsinage(this.Db, this.Session);
                    break;

                case JobBatchType.Assemblage:
                    this.jobBatchProduction = new JobBatchProductionAssemblage(this.Db, this.Session);
                    this.jobBatchMOD = new JobBatchMODAssemblage(this.Db, this.Session);
                    break;

                case JobBatchType.Melange:
                    this.jobBatchProduction = new JobBatchProductionMelange(this.Db, this.Session);
                    this.jobBatchMOD = new JobBatchMODMelange(this.Db, this.Session);
                    break;

                default:
                    throw new BLException(string.Format("Aucune implémentation du type de JobBatch {0}", this.jobBatchType.ToString()));
            }
        }
        private void GetWeekJobBatchs(string jobBatchJobNum, DateTime fromDate, DateTime toDate)
        {
            DataTable dtShift = new DataTable();
            dtShift.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("ID", typeof(int)),
                new DataColumn("Shift", typeof(string))
            });
            dtShift.Rows.Add(1, "Jour");
            dtShift.Rows.Add(2, "Soir");
            dtShift.Rows.Add(3, "Nuit");
            IEnumerable<UD105> uD105s = (from ud in this.Db.UD105.AsEnumerable()
                                         join sf in dtShift.AsEnumerable() on new { Shift = ud.Character03 }
                                                                       equals new { Shift = sf.Field<string>("Shift") }
                                         join lg in this.Db.UD105A.AsEnumerable() on new { ud.Company, ud.Key1, ud.Key2, ud.Key3, ud.Key4, ud.Key5 }
                                                                              equals new { lg.Company, lg.Key1, lg.Key2, lg.Key3, lg.Key4, lg.Key5 }
                                         where ud.Company == this.Session.CompanyID &&
                                               ud.Key1 == jobBatchJobNum &&
                                               ud.Date01 != null &&
                                               ud.Date01 >= fromDate.Date &&
                                               ud.Date01 <= toDate.Date
                                         orderby ud.Date01, sf.Field<int>("ID"), ud.CheckBox01
                                         select ud);
            var jbOrd = (from jb in uD105s
                         group jb by new
                         {
                             DateJobBatch = jb.Date01,
                             Shift = jb.Character03
                         } into grp
                         select new
                         {
                             grp.Key.DateJobBatch,
                             grp.Key.Shift,
                             Valide = grp.Min(r => r.CheckBox02),
                             Soumis = grp.Min(r => r.CheckBox01)
                         });
            DataTable dtWeekJobBatchs = GetDataTable("WeekJobBatchs");
            foreach(var jb in jbOrd)
            {
                dtWeekJobBatchs.Rows.Add(jb.DateJobBatch, jb.Shift, jb.Valide, jb.Soumis);
            }
            MergeDataTable(dtWeekJobBatchs, true);
        }
        public DataSet GetWeekJobBatchs(DataSet iDataSet, string jobBatchJobNum, DateTime fromDate, DateTime toDate)
        {
            iDataSet = this.operationInteraction.LoadInternal(iDataSet);
            LoadDataSet(iDataSet);
            GetWeekJobBatchs(jobBatchJobNum, fromDate, toDate);
            return this.dsMiscAction;
        }
        public DataSet SubmitLabor(DataSet dsJobBatch, DataSet iDataSet)
        {
            iDataSet = this.operationInteraction.LoadInternal(iDataSet);
            LoadDataSet(iDataSet);
            this.jobBatchProduction.SubmitLabor(dsJobBatch, this.dsMiscAction, this.operationInteraction);
            return this.dsMiscAction;
        }
        public DataSet CalculateLabor(DataSet dsJobBatch, DataSet iDataSet)
        {
            iDataSet = operationInteraction.LoadInternal(iDataSet);
            LoadDataSet(iDataSet);
            ClearRows(new string[] { "JobBatch", "WeekJobBatchs" });
            this.jobBatchMOD.ProcessLaborDtl(dsJobBatch, operationInteraction, ref this.dsMiscAction);
            this.jobBatchProduction.Distribute(dsJobBatch, ref this.dsMiscAction);
            return this.dsMiscAction;
        }
        public void GetLaborHedSeqForPayrollDate(string empID, DateTime payrollDate, out int hedSeq)
        {
            hedSeq = -1;
            Erp.Tablesets.LaborTableset dsLabor = new LaborTableset();
            Erp.Contracts.LaborSvcContract svcLabor = Ice.Assemblies.ServiceRenderer.GetService<Erp.Contracts.LaborSvcContract>(this.Db);
            try
            {
                if (!GetLaborHedSeq(empID, payrollDate, out hedSeq))
                {
                    svcLabor.GetNewLaborHed1(ref dsLabor, empID, false, payrollDate);
                    svcLabor.Update(ref dsLabor);
                    if (!GetLaborHedSeq(empID, payrollDate, out hedSeq))
                    {
                        throw new BLException("Impossible de créer l'entrée de temps.");
                    }
                }
                
            }
            catch (Exception) { hedSeq = -1; }
            finally
            {
                dsLabor = null;
                svcLabor.Dispose();
                svcLabor = null;
            }
        }

        public void EmployeeExist(string empID, out bool exist)
        {
            exist = (from emp in this.Db.EmpBasic
                     where emp.EmpID == empID &&
                           emp.Company == this.Session.CompanyID &&
                           emp.EmpStatus == "A"
                     select emp).FirstOrDefault() != null;
        }

        public void GetValidationSelection(string jobBatchJobNum, string dateSelection, string shift)
        {
            DateTime dtSelection;
            if(!DateTime.TryParse(dateSelection, out dtSelection))
            {
                throw new BLException("Veuillez entrer une date valide.");
            }
            string[] shifts = new string[] { "Jour", "Soir", "Nuit" };
            if(!shifts.Contains(shift))
            {
                throw new BLException("Veuilez entrer un quart de travail valide.");
            }
            IEnumerable<LaborDtl> laborDtls = GetPoincons(jobBatchJobNum, dtSelection, shift);
            //CheckLaborDtlDate(laborDtls, dtSelection);
        }

        public IEnumerable<LaborDtl> GetPoincons(string jobBatchJobNum, DateTime dateSelection, string shift)
        {
            IEnumerable<LaborDtl> lDtls = (from ud in this.Db.UD105.AsEnumerable()
                                           join lb in this.Db.LaborDtl.AsEnumerable() on new { ud.Company, RefJobBatch = string.Join("-", new string[] { ud.Key1, ud.Key2 }) }
                                                                                  equals new { lb.Company, RefJobBatch = lb.UDField("UD_RefJobBatch_c", false).ToString() }
                                           where ud.Company == this.Session.CompanyID &&
                                                 ud.Key1 == jobBatchJobNum &&
                                                 ud.Date01 == dateSelection.Date &&
                                                 ud.Character03 == shift &&
                                                 ud.CheckBox01 == false &&
                                                 lb.ActiveTrans == false
                                           select lb);
            return lDtls;
        }

        private void CheckLaborDtlDate(IEnumerable<LaborDtl> laborDtls, DateTime dateSelection)
        {
            IEnumerable<LaborDtl> ldWrongDate = laborDtls.Where(tt => tt.PayrollDate != dateSelection.Date);
            if (ldWrongDate.Any())
            {
                Erp.Tablesets.LaborTableset dsLabor = new LaborTableset();
                Erp.Contracts.LaborSvcContract svcLabor = Ice.Assemblies.ServiceRenderer.GetService<Erp.Contracts.LaborSvcContract>(this.Db);
                try
                {
                    using (var txScopeChangeLaborDtlHedSeq = IceContext.CreateDefaultTransactionScope())
                    {
                        foreach (LaborDtl laborDtl in ldWrongDate)
                        {
                            ChangeLaborDtlHedSeq(laborDtl, dateSelection, svcLabor, dsLabor);
                        }
                        txScopeChangeLaborDtlHedSeq.Complete();
                    }
                }
                catch (Exception) { return; }
                finally
                {
                    dsLabor = null;
                    svcLabor.Dispose();
                    svcLabor = null;
                }
            }
        }

        private void ChangeLaborDtlHedSeq(LaborDtl laborDtl, DateTime dateSelection, Erp.Contracts.LaborSvcContract svcLabor, Erp.Tablesets.LaborTableset dsLabor)
        {
            int hedSeq;
            if(GetLaborHedSeq(laborDtl.EmployeeNum, dateSelection, out hedSeq))
            {
                laborDtl.LaborHedSeq = hedSeq;
                laborDtl.PayrollDate = dateSelection.Date;
                laborDtl.ClockInDate = dateSelection.Date;
                this.Db.Validate<LaborDtl>(laborDtl);
            }
            else
            {
                svcLabor.GetNewLaborHed1(ref dsLabor, laborDtl.EmployeeNum, false, dateSelection.Date);
                svcLabor.Update(ref dsLabor);
                if (GetLaborHedSeq(laborDtl.EmployeeNum, dateSelection, out hedSeq))
                {
                    laborDtl.LaborHedSeq = hedSeq;
                    laborDtl.PayrollDate = dateSelection.Date;
                    laborDtl.ClockInDate = dateSelection.Date;
                    this.Db.Validate<LaborDtl>(laborDtl);
                }
            }
        }

        private bool GetLaborHedSeq(string empID, DateTime payrollDate, out int hedSeq)
        {
            hedSeq = -1;
            LaborHed lHed = (from lh in this.Db.LaborHed
                             where lh.Company == this.Session.CompanyID &&
                                   lh.EmployeeNum == empID &&
                                   lh.PayrollDate == payrollDate.Date
                             select lh).FirstOrDefault();
            if(lHed != null)
            {
                hedSeq = lHed.LaborHedSeq;
            }
            return hedSeq != -1;
        }

        public void GetRefPoinconForNewPunch(string jobBatchJobNum, string dateJobBatch, string resourceID, string quart, out string refPoincon)
        {
            refPoincon = string.Empty;
            DateTime dtJobBatch;
            if (!DateTime.TryParse(dateJobBatch, out dtJobBatch))
            {
                throw new BLException("Veuillez entrer une date valide.");
            }
            if(!GetRefPoincon(jobBatchJobNum, dtJobBatch, resourceID, quart, out refPoincon))
            {
                CreateJobBatchForNewPunch(jobBatchJobNum, dtJobBatch, resourceID, quart);
                GetRefPoincon(jobBatchJobNum, dtJobBatch, resourceID, quart, out refPoincon);
            }
        }

        private bool GetRefPoincon(string jobBatchJobNum, DateTime dateJobBatch, string resourceID, string quart, out string refPoincon)
        {
            refPoincon = string.Empty;
            var jobBatch = (from ud in this.Db.UD105
                            where ud.Company == this.Session.CompanyID &&
                                  ud.Key1 == jobBatchJobNum &&
                                  ud.Date01 == dateJobBatch &&
                                  ud.Character02 == resourceID &&
                                  ud.Character03 == quart
                            select ud).FirstOrDefault();
            if(jobBatch != null)
            {
                refPoincon = string.Format("{0}-{1}", jobBatch.Key1, jobBatch.Key2);
                return true;
            }
            return false;
        }

        private void CreateJobBatchForNewPunch(string jobBatchJobNum, DateTime dateJobBatch, string resourceID, string quart)
        {
            Ice.Tablesets.UD105Tableset dsJobBatch = new UD105Tableset();
            Ice.Contracts.UD105SvcContract svcJobBatch = Ice.Assemblies.ServiceRenderer.GetService<Ice.Contracts.UD105SvcContract>(this.Db);
            try
            {
                svcJobBatch.GetaNewUD105(ref dsJobBatch);
                Ice.Tablesets.UD105Row udJobBatch = dsJobBatch.UD105.Where(tt => tt.Added()).FirstOrDefault();
                if( udJobBatch == null)
                {
                    throw new BLException("Impossible de créer le job batch.");
                }
                udJobBatch.Key1 = jobBatchJobNum;
                udJobBatch.Date01 = dateJobBatch;
                udJobBatch.Character02 = resourceID;
                udJobBatch.Character03 = quart;
                svcJobBatch.Update(ref dsJobBatch);
            }
            catch (Exception) { return; }
            finally
            {
                dsJobBatch = null;
                svcJobBatch.Dispose();
                svcJobBatch = null;
            }
        }

        public void GetRefBatchLigneOperateur(string refPoincon, string empID, string oprSeq, out string refBatchLigneOperateur)
        {
            refBatchLigneOperateur = string.Empty;
            string[] refP = refPoincon.Split('-');
            if(refP.Length != 2)
            {
                throw new BLException("La référence au job batch est invalide.");
            }
            if(!GetRefBatchLigneOperateur(refP[0], refP[1], empID, oprSeq, out refBatchLigneOperateur))
            {
                CreateRefBatchLigneOperateur(refP[0], refP[1], empID, oprSeq);
                GetRefBatchLigneOperateur(refP[0], refP[1], empID, oprSeq, out refBatchLigneOperateur);
            }
        }

        private bool GetRefBatchLigneOperateur(string refKey1, string refKey2, string empID, string oprSeq, out string refBatchLigneOperateur)
        {
            refBatchLigneOperateur = string.Empty;
            var operateur = (from ud in this.Db.UD105A
                            where ud.Company == this.Session.CompanyID &&
                                  ud.Key1 == refKey1 &&
                                  ud.Key2 == refKey2 &&
                                  ud.ChildKey1 == "Operateur" &&
                                  ud.ChildKey3 == empID &&
                                  ud.Character02 == oprSeq
                            select ud).FirstOrDefault();
            if (operateur != null)
            {
                refBatchLigneOperateur = operateur.SysRowID.ToString();
                return true;
            }
            return false;
        }

        private void CreateRefBatchLigneOperateur(string refKey1, string refKey2, string empID, string oprSeq)
        {
            Ice.Tablesets.UD105Tableset dsJobBatch = new UD105Tableset();
            Ice.Contracts.UD105SvcContract svcJobBatch = Ice.Assemblies.ServiceRenderer.GetService<Ice.Contracts.UD105SvcContract>(this.Db);
            try
            {
                dsJobBatch = svcJobBatch.GetByID(refKey1, refKey2, string.Empty, string.Empty, string.Empty);
                svcJobBatch.GetaNewUD105A(ref dsJobBatch, refKey1, refKey2, string.Empty, string.Empty, string.Empty);
                Ice.Tablesets.UD105ARow udJobBatch = dsJobBatch.UD105A.Where(tt => tt.Added()).FirstOrDefault();
                if (udJobBatch == null)
                {
                    throw new BLException("Impossible de créer le job batch.");
                }
                string empName = GetEmployeeName(empID);
                udJobBatch.ChildKey1 = "Operateur";
                udJobBatch.ChildKey3 = empID;
                udJobBatch.Character01 = empName;
                udJobBatch.Character02 = oprSeq;
                udJobBatch.CheckBox02 = true;
                svcJobBatch.Update(ref dsJobBatch);
            }
            catch (Exception) { return; }
            finally
            {
                dsJobBatch = null;
                svcJobBatch.Dispose();
                svcJobBatch = null;
            }
        }

        private string GetEmployeeName(string empID)
        {
            var empl = (from emp in this.Db.EmpBasic
                       where emp.EmpID == empID &&
                             emp.Company == this.Session.CompanyID &&
                             emp.EmpStatus == "A"
                       select emp).FirstOrDefault();
            if(empl != null)
            {
                return empl.Name;
            }
            return "";
        }

        public bool JobValidForJobBatchWithOprSeq(string jobBatchJobNum, string jobNum, string oprSeq, out string vMessage)
        {
            vMessage = string.Empty;
            try
            {
                int opSeq;
                if(!int.TryParse(oprSeq, out opSeq))
                {
                    throw new BLException("Opération invalide, veuillez contacter l'administrateur du système");
                }
                JobValidForJobBatchWithOprSeq(jobBatchJobNum, jobNum, opSeq);
                return true;
            }
            catch(Exception ex)
            {
                vMessage = ex.Message;
                return false;
            }
        }

        private void JobValidForJobBatchWithOprSeq(string jobBatchJobNum, string jobNum, int oprSeq)
        {
            var jobHed = (from jb in this.Db.JobOper
                          join jo in this.Db.JobOper on new { jb.Company, jb.OpCode }
                                                 equals new { jo.Company, jo.OpCode }
                          join jh in this.Db.JobHead on new { jo.Company, jo.JobNum }
                                                 equals new { jh.Company, jh.JobNum }
                          where jb.Company == this.Session.CompanyID &&
                                jb.JobNum == jobBatchJobNum &&
                                jb.OprSeq == oprSeq &&
                                jh.JobNum == jobNum
                          select jh).FirstOrDefault();
            if(jobHed == null)
            {
                throw new BLException("Le bon de travail n'existe pas ou ne comporte pas cette opération.");
            }
            if (jobHed.JobClosed)
            {
                throw new BLException("Le bon de travail est fermé.");
            }
            if (!jobHed.JobReleased)
            {
                throw new BLException("Le bon de travail n'a pas été relâché pour la production.");
            }
        }

        public bool JobValidForJobBatchSetupWithOpCode(string jobBatchJobNum, string jobNum, string opCode, out string vMessage)
        {
            OperationInteraction opInteraction = new OperationInteraction(this.Db, this.Session);
            opInteraction.GetOperationInteractions();
            vMessage = string.Empty;
            try
            {
                if (!opInteraction.OperationIs("IsSetup", opCode))
                {
                    throw new BLException("Opération invalide, veuillez contacter l'administrateur du système");
                }
                string opRef;
                if(!opInteraction.GetOperationSetupRef(opCode, out opRef))
                {
                    throw new BLException("Opération invalide, veuillez contacter l'administrateur du système");
                }
                JobValidForJobBatchSetupWithOpCode(jobBatchJobNum, jobNum, opRef);
                return true;
            }
            catch (Exception ex)
            {
                vMessage = ex.Message;
                return false;
            }
        }
        private void JobValidForJobBatchSetupWithOpCode(string jobBatchJobNum, string jobNum, string opCode)
        {

            var jobHed = (from jb in this.Db.JobOper
                          join jo in this.Db.JobOper on new { jb.Company, jb.OpCode }
                                                 equals new { jo.Company, jo.OpCode }
                          join jh in this.Db.JobHead on new { jo.Company, jo.JobNum }
                                                 equals new { jh.Company, jh.JobNum }
                          where jb.Company == this.Session.CompanyID &&
                                jb.JobNum == jobBatchJobNum &&
                                jb.OpCode == opCode &&
                                jh.JobNum == jobNum
                          select jh).FirstOrDefault();
            if (jobHed == null)
            {
                throw new BLException("Le bon de travail n'existe pas ou ne comporte pas cette opération.");
            }
            if (jobHed.JobClosed)
            {
                throw new BLException("Le bon de travail est fermé.");
            }
            if (!jobHed.JobReleased)
            {
                throw new BLException("Le bon de travail n'a pas été relâché pour la production.");
            }
        }

        public bool CheckAllClockedOut(string refJobBatchs, out string message)
        {
            message = string.Empty;
            bool clockedOut = true;
            string[] refJB = refJobBatchs.Split('~');
            if (!refJB.Any())
            {
                return clockedOut;
            }
            var labor = (from lbr in (from lb in this.Db.LaborDtl.AsEnumerable()
                                      join em in this.Db.EmpBasic.AsEnumerable() on new { lb.Company, lb.EmployeeNum }
                                                                             equals new { em.Company, EmployeeNum = em.EmpID }
                                      join rs in (from ud in this.Db.UD105
                                                  join rs in this.Db.Resource on new { ud.Company, ResourceID = ud.Character02 }
                                                                          equals new { rs.Company, rs.ResourceID }
                                                  where ud.Company == this.Session.CompanyID
                                                  select new
                                                  {
                                                      RefJobBatch = ud.Key1 + "-" + ud.Key2,
                                                      Resource = rs.Description
                                                  }).AsEnumerable() on new { RefJobBatch = lb.UDField<string>("UD_RefJobBatch_c", false) }
                                                                equals new { rs.RefJobBatch }
                                      where lb.Company == this.Session.CompanyID &&
                                          refJB.Contains(lb.UDField<string>("UD_RefJobBatch_c", false)) &&
                                          lb.ActiveTrans == true
                                      select new
                                      {
                                          Employee = em.Name,
                                          rs.Resource
                                      }).AsEnumerable()
                         group lbr by new
                         {
                             lbr.Employee,
                             lbr.Resource
                         } into lb
                         select new
                         {
                             lb.Key.Employee,
                             lb.Key.Resource
                         });
            if (labor.Any())
            {
                clockedOut = false;
                foreach(var lbr in labor)
                {
                    if (!string.IsNullOrEmpty(message))
                    {
                        message += "\r\n";
                    }
                    message += string.Format("L'utilisateur {0} n'a pas poinçonné sa sortie dans l'équipement {1}", lbr.Employee, lbr.Resource);
                }
            }
            return clockedOut;
        }

    }
}

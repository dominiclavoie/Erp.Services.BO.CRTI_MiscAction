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

namespace Erp.BO.CRTI_MiscAction
{
    class GestionJobBatch : BaseMiscAction, IMiscAction
    {

        public GestionJobBatch(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session) { }

        public override DataTable[] GetDataTable()
        {
            DataTable dt = new DataTable("EquipementJobBatch");
            dt.Locale = CultureInfo.InvariantCulture;
            dt.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("ResourceID", typeof(string)),
                new DataColumn("Description", typeof(string))
            });
            DataTable dt2 = new DataTable("OperationJobBatch");
            dt2.Locale = CultureInfo.InvariantCulture;
            dt2.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("OprSeq", typeof(string)),
                new DataColumn("OpCode", typeof(string)),
                new DataColumn("Description", typeof(string))
            });
            DataTable dt3 = new DataTable("OperationComplementaire");
            dt3.Locale = CultureInfo.InvariantCulture;
            dt3.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("OpComplementaire", typeof(string)),
                new DataColumn("Description", typeof(string))
            });
            DataTable dt4 = new DataTable("LastOperationLotNum");
            dt4.Locale = CultureInfo.InvariantCulture;
            dt4.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("LotNum", typeof(string)),
                new DataColumn("InfoLot", typeof(string))
            });
            DataTable dt5 = new DataTable("BTJobFictive");
            dt5.Locale = CultureInfo.InvariantCulture;
            dt5.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("Type", typeof(string)),
                new DataColumn("No BT", typeof(string))
            });
            DataTable dt6 = new DataTable("OperationBarlist");
            dt6.Locale = CultureInfo.InvariantCulture;
            dt6.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("OprSeq", typeof(string)),
                new DataColumn("OpCode", typeof(string)),
                new DataColumn("Description", typeof(string))
            });
            return new DataTable[] { dt, dt2, dt3, dt4, dt5, dt6 };
        }

        private void GetListForJobBatch(string jobNum)
        {
            DataTable dtEquipement = GetDataTable("EquipementJobBatch");
            var ress = (from jo in this.Db.JobOper.AsEnumerable()
                        join jr in this.Db.JobOpDtl.AsEnumerable() on new { jo.Company, jo.JobNum, jo.AssemblySeq, jo.OprSeq }
                                                               equals new { jr.Company, jr.JobNum, jr.AssemblySeq, jr.OprSeq }
                        join rs in this.Db.Resource.AsEnumerable() on new { jr.Company, jr.ResourceGrpID }
                                                               equals new { rs.Company, rs.ResourceGrpID }
                        where jo.Company == this.Session.CompanyID &&
                              jo.JobNum == jobNum &&
                              !(from ud in this.Db.UD12.AsEnumerable()
                               where ud.Company == this.Session.CompanyID &&
                                     ud.Key2 == "IsOperGeneral"
                               select ud.Key1).Contains(jo.OpCode) &&
                              rs.Inactive == false &&
                              rs.Location == true
                        group rs by new
                        {
                            rs.ResourceID,
                            rs.Description
                        } into grp
                        select new
                        {
                            grp.Key.ResourceID,
                            grp.Key.Description
                        });
            foreach (var res in ress)
            {
                dtEquipement.Rows.Add(res.ResourceID, res.Description);
            }
            MergeDataTable(dtEquipement, true);
        }

        private void GetOperationsForJobBatch(string jobNum)
        {
            DataTable dtOperation = GetDataTable("OperationJobBatch");
            var oprs = (from jo in this.Db.JobOper.AsEnumerable()
                        where jo.Company == this.Session.CompanyID &&
                              jo.JobNum == jobNum &&
                              jo.AssemblySeq == 0
                        select new
                        {
                            jo.OprSeq,
                            jo.OpCode,
                            jo.OpDesc
                        });
            foreach (var opr in oprs)
            {
                dtOperation.Rows.Add(opr.OprSeq.ToString(), opr.OpCode, opr.OpDesc);
            }
            MergeDataTable(dtOperation, true);
        }

        private void GetOperationsForBarlistID(string jobBatchJobNum, string idLigne, out bool hasMany)
        {
            hasMany = false;
            DataTable dtOperation = GetDataTable("OperationBarlist");
            if (GetBarlistLineByID(idLigne, out string jobNum, out string opCode, out bool setupAvailable))
            {
                string[] oprs = (from jo in this.Db.JobOper.AsEnumerable()
                                 where jo.Company == this.Session.CompanyID &&
                                  jo.JobNum == jobNum &&
                                  jo.AssemblySeq == 0 &&
                                  jo.OpCode != opCode &&
                                  jo.LaborEntryMethod == "X" &&
                                  (from jb in this.Db.JobOper.AsEnumerable()
                                   where jb.Company == this.Session.CompanyID &&
                                         jb.JobNum == jobBatchJobNum &&
                                         jb.AssemblySeq == 0
                                   select jb.OpCode).Contains(jo.OpCode)
                                 select jo.OpCode).ToArray();
                hasMany = oprs.Any();
                string[] opCodes = new string[] { opCode };
                if (hasMany)
                {
                    Array.Resize(ref opCodes, opCodes.Length + oprs.Length);
                    Array.Copy(oprs, 0, opCodes, opCodes.Length - oprs.Length, oprs.Length);
                }
                var opers = (from jb in this.Db.JobOper.AsEnumerable()
                             where jb.Company == this.Session.CompanyID &&
                                   jb.JobNum == jobBatchJobNum &&
                                   jb.AssemblySeq == 0 &&
                                   opCodes.Contains(jb.OpCode)
                             select new
                             {
                                 jb.OprSeq,
                                 jb.OpCode,
                                 jb.OpDesc
                             });
                foreach (var opr in opers)
                {
                    dtOperation.Rows.Add(opr.OprSeq.ToString(), opr.OpCode, opr.OpDesc);
                }
            }
            MergeDataTable(dtOperation, true);
        }

        private void GetAllOperationComplementaire()
        {
            DataTable dtOperation = GetDataTable("OperationComplementaire");
            var ops = (from Val in Db.PcLookupTblValues
                       join Filt in Db.PcLookupTblValues on new { Val.Company, Val.RowNum, Val.LookupTblID } equals new { Filt.Company, Filt.RowNum, Filt.LookupTblID }
                       join Act in Db.PcLookupTblValues on new { Val.Company, Val.RowNum, Val.LookupTblID } equals new { Act.Company, Act.RowNum, Act.LookupTblID }
                       join Desc in Db.PcLookupTblValues on new { Val.Company, Val.RowNum, Val.LookupTblID } equals new { Desc.Company, Desc.RowNum, Desc.LookupTblID }
                       where Val.LookupTblID == "OperationComplementaire"
                       && Val.ColName == "OpComplementaire"
                       && Desc.ColName == "Description"
                       && Filt.ColName == "OpCode"
                       && Act.ColName == "Actif"
                       && Act.DataValueBool == true
                       group new { Val, Desc } by new { OpComplementaire = Val.DataValue, Description = Desc.DataValue } into grp
                       orderby grp.Key.Description
                       select new
                       {
                           grp.Key.OpComplementaire,
                           grp.Key.Description
                       });
            foreach (var op in ops)
            {
                dtOperation.Rows.Add(op.OpComplementaire.ToString(), op.Description.ToString());
            }
            MergeDataTable(dtOperation, true);
        }

        private void GetOperationComplementaire(string opCode)
        {
            DataTable dtOperation = GetDataTable("OperationComplementaire");
            var ops = (from Val in Db.PcLookupTblValues
                       join Filt in Db.PcLookupTblValues on new { Val.Company, Val.RowNum, Val.LookupTblID } equals new { Filt.Company, Filt.RowNum, Filt.LookupTblID }
                       join Act in Db.PcLookupTblValues on new { Val.Company, Val.RowNum, Val.LookupTblID } equals new { Act.Company, Act.RowNum, Act.LookupTblID }
                       join Desc in Db.PcLookupTblValues on new { Val.Company, Val.RowNum, Val.LookupTblID } equals new { Desc.Company, Desc.RowNum, Desc.LookupTblID }
                       where Val.LookupTblID == "OperationComplementaire"
                       && Val.ColName == "OpComplementaire"
                       && Desc.ColName == "Description"
                       && Filt.ColName == "OpCode"
                       && Filt.DataValue == opCode
                       && Act.ColName == "Actif"
                       && Act.DataValueBool == true
                       orderby Val.DataValue
                       select new {
                           OpComplementaire = Val.DataValue,
                           Description = Desc.DataValue
                       });
            foreach (var op in ops)
            {
                dtOperation.Rows.Add(op.OpComplementaire.ToString(), op.Description.ToString());
            }
            MergeDataTable(dtOperation, true);
        }

        private List<OperationLotNum> GetOperationLotNumFromLastOperation(string jobNum, string opCode)
        {
            return (from ud in this.Db.UD105A
                    join op in ((from lo in this.Db.JobOper
                                 join jo in (from jo in this.Db.JobOper
                                             where jo.Company == this.Session.CompanyID &&
                                                   jo.JobNum == jobNum &&
                                                   jo.OpCode == opCode
                                             select new
                                             {
                                                 jo.Company,
                                                 jo.JobNum,
                                                 jo.AssemblySeq,
                                                 jo.OprSeq
                                             }).AsEnumerable() on new { lo.Company, lo.JobNum, lo.AssemblySeq }
                                                           equals new { jo.Company, jo.JobNum, jo.AssemblySeq }
                                 where lo.OprSeq < jo.OprSeq
                                 orderby lo.OprSeq descending
                                 select new
                                 {
                                     lo.Company,
                                     lo.JobNum,
                                     OprSeq = lo.OprSeq.ToString()
                                 }).Take(1)).AsEnumerable() on new { ud.Company, JobNum = ud.Character07, OprSeq = ud.Character08 }
                                                        equals new { op.Company, op.JobNum, op.OprSeq }
                    where !string.IsNullOrEmpty(ud.Character05) &&
                          ud.Company == this.Session.CompanyID &&
                          ud.ChildKey1 == "Production"
                    group ud by ud.Character05 into g
                    select new OperationLotNum
                    {
                        LotNum = g.Key,
                        ProdEntries = g.ToList()
                    }).ToList();
        }

        private void GetLotNumForLastOperation(string jobNum, string opCode)
        {
            DataTable dtLastOperationLotNum = GetDataTable("LastOperationLotNum");
            List<OperationLotNum> operationLotNums = GetOperationLotNumFromLastOperation(jobNum, opCode);
            foreach (OperationLotNum operationLotNum in operationLotNums)
            {
                dtLastOperationLotNum.Rows.Add(operationLotNum.LotNum, operationLotNum.LotNum);
            }
            MergeDataTable(dtLastOperationLotNum, true);
        }

        public bool CheckCanDeleteLotNumForOperation(string idLigne, string lotNum, string rowID, out string message)
        {
            message = "";
            var barlist = (from ud in this.Db.UD104
                           where ud.Key2 == "JobOper" &&
                                 ud.ShortChar01 == idLigne
                           select ud).FirstOrDefault();
            if(barlist == null)
            {
                return true;
            }
            JobOper jobOper;
            if (!NextOperIsUsePrevOpLot(barlist.Key3, barlist.Key4, out jobOper))
            {
                return true;
            }
            if(!IsLastWithLotNum(idLigne, lotNum, rowID))
            {
                return true;
            }
            string[] prodTypes = new string[] { "Production", "RebutProduction" };
            var lotExists = (from ud in this.Db.UD104.AsEnumerable()
                             join jb in this.Db.UD105A.AsEnumerable() on new { ud.Company, IDLigne = ud.ShortChar01, JobNum = ud.Key3, OprSeq = ud.Key4 }
                                                   equals new { jb.Company, IDLigne = jb.ChildKey3, JobNum = jb.Character07, OprSeq = jb.Character08 }
                             join hjb in this.Db.UD105.AsEnumerable() on new { jb.Company, jb.Key1, jb.Key2, jb.Key3, jb.Key4, jb.Key5 }
                                                   equals new { hjb.Company, hjb.Key1, hjb.Key2, hjb.Key3, hjb.Key4, hjb.Key5 }
                             where ud.Company == jobOper.Company &&
                                   ud.Character09 == jobOper.SysRowID.ToString() &&
                                   ud.Key2 == "JobOper" &&
                                   prodTypes.Contains(jb.ChildKey1) &&
                                   jb.Character05 == lotNum
                             select new
                             {
                                 DateJB = (hjb.Date01 ?? DateTime.Now).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                                 Quart = hjb.Character03,
                                 Equipement = hjb.Character02,
                                 Type = jb.ChildKey1
                             });
            if (lotExists.Any())
            {
                message = string.Format("Des entrées d'opération subséquentes utilise ce lot, vous devez d'abord les supprimer. Il s'agit de l'opération «{0}» et les entrées suivantes:", jobOper.OpDesc);
                foreach(var lot in lotExists)
                {
                    message += string.Format("\r\nDate de l'entrée: {0}, Quart de travail: {1}, Equipement: {2}, Entrée: {3}", lot.DateJB, lot.Quart, lot.Equipement, lot.Type);
                }
                return false;
            }
            return true;
        }
        private bool IsLastWithLotNum(string idLigne, string lotNum, string currentRowID)
        {
            return (from jb in this.Db.UD105A.AsEnumerable()
                    where jb.Company == this.Session.CompanyID &&
                        jb.ChildKey3 == idLigne &&
                        jb.ChildKey1 == "Production" &&
                        jb.Character05 == lotNum &&
                        jb.SysRowID.ToString() != currentRowID
                    select jb).FirstOrDefault() == null;
        }
        private bool NextOperIsUsePrevOpLot(string jobNum, string oprSeq, out JobOper jobOper)
        {
            jobOper = null;
            var oper = (from lo in this.Db.JobOper
                        join jo in (from jo in this.Db.JobOper
                                    where jo.Company == this.Session.CompanyID &&
                                          jo.JobNum == jobNum &&
                                          jo.OprSeq.ToString() == oprSeq
                                    select new
                                    {
                                        jo.Company,
                                        jo.JobNum,
                                        jo.AssemblySeq,
                                        jo.OprSeq
                                    }).AsEnumerable() on new { lo.Company, lo.JobNum, lo.AssemblySeq }
                                                  equals new { jo.Company, jo.JobNum, jo.AssemblySeq }
                        where lo.OprSeq > jo.OprSeq
                        orderby lo.OprSeq ascending
                        select lo).FirstOrDefault();
            if(oper == null)
            {
                return false;
            }
            var isUsePrevOpLot = (from oi in this.Db.UD12
                     where oi.Company == oper.Company &&
                           oi.Key1 == oper.OpCode &&
                           oi.Key2 == "UsePrevOpLot"
                     select oi).FirstOrDefault();
            if(isUsePrevOpLot == null)
            {
                return false;
            }
            jobOper = oper;
            return true;
        }

        private void GetBTJobFictive()
        {
            DataTable dtBTJobFictive = GetDataTable("BTJobFictive");
            string[] validTypes = new string[]
            {
                "VRODDroite",
                "ProfileBrut",
                "Melange",
                "Volvo",
                "Hydro",
                "Pultrusion"
            };
            var config = (from cg in this.Db.JCSyst
                          where cg.Company == this.Session.CompanyID
                          select cg).FirstOrDefault();
            string configBT = config == null ? string.Empty : config.UDField<string>("UD_JobBatchFictif_c", false);
            Dictionary<string, string> jbFictifs = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(configBT))
            {
                try { jbFictifs = configBT.Split('|').Select(item => item.Split('~')).ToDictionary(t => t[0], t => t[1]); }
                catch (Exception) { jbFictifs = new Dictionary<string, string>(); }
            }
            foreach(string validType in validTypes)
            {
                dtBTJobFictive.Rows.Add(validType, jbFictifs.ContainsKey(validType) ? jbFictifs[validType] : "");
            }
            MergeDataTable(dtBTJobFictive, true);
        }

        public bool GetBarlistLineByID(string IDLigne, out string jobNum, out string opCode, out bool setupAvailable)
        {
            jobNum = string.Empty;
            opCode = string.Empty;
            setupAvailable = false;
            var barlistLine = (from ud in this.Db.UD104
                               join jo in this.Db.JobOper on new { ud.Company, Type = ud.Key2, JobNum = ud.Key3, AssemblySeq = 0, Opr = ud.Key4 }
                                                      equals new { jo.Company, Type = "JobOper", jo.JobNum, jo.AssemblySeq, Opr = jo.OprSeq.ToString() }
                               where ud.Company == this.Session.CompanyID &&
                                     ud.ShortChar01 == IDLigne
                               select new
                               {
                                   jo.JobNum,
                                   jo.OpCode,
                                   SetupAvailable = jo.EstSetHoursPerMch > 0m
                               }).FirstOrDefault();
            if( barlistLine != null)
            {
                jobNum = barlistLine.JobNum;
                opCode = barlistLine.OpCode;
                setupAvailable = barlistLine.SetupAvailable;
                return true;
            }
            return false;
        }

        public bool SetupAvailableForOpCode(string jobNum, string opCode)
        {
            var oper = (from jo in this.Db.JobOper
                        where jo.Company == this.Session.CompanyID &&
                              jo.JobNum == jobNum &&
                              jo.OpCode == opCode
                        select new { SetupAvailable = jo.EstSetHoursPerMch > 0m }).FirstOrDefault();
            if(oper == null)
            {
                return false;
            }
            return oper.SetupAvailable;
        }

        public bool GetOperationGeneralByID(string jobBatchJobNum, string ID)
        {
            var oper = (from ud in this.Db.UD12
                        join jo in this.Db.JobOper on new { ud.Company, OpCode = ud.Key1 }
                                               equals new { jo.Company, jo.OpCode }
                        where ud.Company == this.Session.CompanyID &&
                              ud.Key2 == "IsOperGeneral" &&
                              jo.JobNum == jobBatchJobNum &&
                              ud.Key1 == ID
                        select ud.Key1);
            return oper.Any();
        }

        public bool GetOperSeq(string jobNum, string opCode, out int oprSeq)
        {
            oprSeq = -1;
            var oper = (from jo in this.Db.JobOper
                        where jo.Company == this.Session.CompanyID &&
                              jo.JobNum == jobNum &&
                              jo.AssemblySeq == 0 &&
                              jo.OpCode == opCode
                        select new { jo.OprSeq }).FirstOrDefault();
            if(oper == null)
            {
                return false;
            }
            oprSeq = oper.OprSeq;
            return true;
        }

        public DataSet GetBTJobFictive(DataSet iDataSet)
        {
            LoadDataSet(iDataSet);
            GetBTJobFictive();
            return this.dsMiscAction;
        }

        public DataSet GetListForJobBatch(string jobNum, DataSet iDataSet)
        {
            LoadDataSet(iDataSet);
            GetListForJobBatch(jobNum);
            return this.dsMiscAction;
        }

        public DataSet GetOperationsForJobBatch(string jobNum, DataSet iDataSet)
        {
            LoadDataSet(iDataSet);
            GetOperationsForJobBatch(jobNum);
            return this.dsMiscAction;
        }

        public DataSet GetOperationsForBarlistID(DataSet iDataSet, string jobBatchJobNum, string idLigne, out bool hasMany)
        {
            LoadDataSet(iDataSet);
            GetOperationsForBarlistID(jobBatchJobNum, idLigne, out hasMany);
            return this.dsMiscAction;
        }

        public DataSet GetOperationComplementaire(string opCode, DataSet iDataSet)
        {
            LoadDataSet(iDataSet);
            GetOperationComplementaire(opCode);
            return this.dsMiscAction;
        }

        public DataSet GetAllOperationComplementaire(DataSet iDataSet)
        {
            LoadDataSet(iDataSet);
            GetAllOperationComplementaire();
            return this.dsMiscAction;
        }

        public DataSet GetLotNumForLastOperation(string jobNum, string opCode, DataSet iDataSet)
        {
            LoadDataSet(iDataSet);
            GetLotNumForLastOperation(jobNum, opCode);
            return this.dsMiscAction;
        }

        private class OperationLotNum
        {
            private string _lotNum;
            private List<UD105A> _prodEntries;

            public string LotNum { get => _lotNum; set => _lotNum = value; }
            public List<UD105A> ProdEntries { get => _prodEntries; set => _prodEntries = value; }
            public OperationLotNum() { }
        }

    }
}

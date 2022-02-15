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
    class GestionLotProduction : BaseMiscAction, IMiscAction
    {

        public GestionLotProduction(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session) {}

        public override DataTable[] GetDataTable()
        {
            DataTable dt = new DataTable("LotProductionProfile");
            dt.Locale = CultureInfo.InvariantCulture;
            dt.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("IDLot", typeof(string)),
                new DataColumn("No lot", typeof(string)),
                new DataColumn("Profilé consommé", typeof(string)),
                new DataColumn("Ratio", typeof(decimal))
            });
            DataTable dt2 = new DataTable("LotAssemblageHydro");
            dt2.Locale = CultureInfo.InvariantCulture;
            dt2.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("PartNum", typeof(string)),
                new DataColumn("Description", typeof(string)),
                new DataColumn("LotNum", typeof(string)),
                new DataColumn("TrackLots", typeof(bool)),
                new DataColumn("QtyPer", typeof(decimal)),
                new DataColumn("MtlSeq", typeof(string)),
                new DataColumn("ScrapQty", typeof(decimal)),
                new DataColumn("EstWIP", typeof(bool)),
                new DataColumn("IdLigneProdFrom", typeof(string)),
                new DataColumn("RefComboLot", typeof(string)),
                new DataColumn("SysRowID", typeof(Guid)){DefaultValue = Guid.NewGuid()}
            });
            return new DataTable[] { dt, dt2 };
        }

        private void GetPartsForLotAssemblageHydro(string idLigne, bool onlyTrackLots)
        {
            DataTable dtLotAssemblageHydro = GetDataTable("LotAssemblageHydro");
            string[] classIds = new string[] { "BSM", "CSTG" };
            var parts = (from ud in this.Db.UD104.AsEnumerable()
                         join jm in this.Db.JobMtl.AsEnumerable() on new { ud.Company, JobNum = ud.Key3, OprSeq = ud.Key4, AssemblySeq = 0 }
                                                              equals new { jm.Company, jm.JobNum, OprSeq = jm.RelatedOperation.ToString(), jm.AssemblySeq }
                         join pt in this.Db.Part.AsEnumerable() on new { jm.Company, jm.PartNum }
                                                            equals new { pt.Company, pt.PartNum }
                         where ud.Key2 == "JobOper" &&
                               ud.ShortChar01 == idLigne &&
                               classIds.Contains(pt.ClassID.ToUpper()) &&
                               (pt.TrackLots || pt.UDField<bool>("UD_TrackLotProduction_c", false)) &&
                               pt.Company == this.Session.CompanyID
                         select new
                         {
                             pt.PartNum,
                             pt.PartDescription,
                             pt.TrackLots,
                             jm.QtyPer,
                             jm.MtlSeq
                         });
            if (onlyTrackLots)
            {
                parts = parts.Where(tt => tt.TrackLots);
            }
            foreach(var part in parts)
            {
                dtLotAssemblageHydro.Rows.Add(part.PartNum, part.PartDescription, "", part.TrackLots, part.QtyPer, part.MtlSeq, 0m);
            }
            MergeDataTable(dtLotAssemblageHydro, true);
        }

        private void GetAllPartsForLotAssemblageHydro(string idLigne, bool onlyTrackLots)
        {
            DataTable dtLotAssemblageHydro = GetDataTable("LotAssemblageHydro");
            string[] classIds = new string[] { "BSM", "CSTG" };
            var parts = (from ud in this.Db.UD104.AsEnumerable()
                         join jm in this.Db.JobMtl.AsEnumerable() on new { ud.Company, JobNum = ud.Key3, AssemblySeq = 0 }
                                                              equals new { jm.Company, jm.JobNum, jm.AssemblySeq }
                         join pt in this.Db.Part.AsEnumerable() on new { jm.Company, jm.PartNum }
                                                            equals new { pt.Company, pt.PartNum }
                         where ud.Key2 == "JobOper" &&
                               ud.ShortChar01 == idLigne &&
                               classIds.Contains(pt.ClassID.ToUpper()) &&
                               (pt.TrackLots || pt.UDField<bool>("UD_TrackLotProduction_c", false)) &&
                               pt.Company == this.Session.CompanyID
                         select new
                         {
                             pt.PartNum,
                             pt.PartDescription,
                             pt.TrackLots,
                             jm.QtyPer,
                             jm.MtlSeq
                         });
            if (onlyTrackLots)
            {
                parts = parts.Where(tt => tt.TrackLots);
            }
            foreach (var part in parts)
            {
                dtLotAssemblageHydro.Rows.Add(part.PartNum, part.PartDescription, "", part.TrackLots, part.QtyPer, part.MtlSeq, 0m);
            }
            MergeDataTable(dtLotAssemblageHydro, true);
        }

        public DataSet GetPartsForLotAssemblageHydro(string idLigne, bool onlyTrackLots, bool allParts, DataSet iDataSet)
        {
            LoadDataSet(iDataSet);
            if(allParts)
            {
                GetAllPartsForLotAssemblageHydro(idLigne, onlyTrackLots);
            }
            else
            {
                GetPartsForLotAssemblageHydro(idLigne, onlyTrackLots);
            }
            return this.dsMiscAction;
        }

        public string GetMtlSeqForPlaceHolder(string idLigne)
        {
            var parts = (from ud in this.Db.UD104.AsEnumerable()
                         join jo in this.Db.JobOper.AsEnumerable() on new { ud.Company, JobNum = ud.Key3, OpSeq = ud.Key4, AssemblySeq = 0 }
                                                               equals new { jo.Company, jo.JobNum, OpSeq = jo.OprSeq.ToString(), jo.AssemblySeq }
                         join oi in this.Db.UD12.AsEnumerable() on new { jo.Company, jo.OpCode, Interaction = "ScrapMtlLot" }
                                                            equals new { oi.Company, OpCode = oi.Key1, Interaction = oi.Key2 }
                         join jm in this.Db.JobMtl.AsEnumerable() on new { jo.Company, jo.AssemblySeq, jo.OprSeq, PartNum = oi.Character01 }
                                                              equals new { jm.Company, jm.AssemblySeq, OprSeq = jm.RelatedOperation, jm.PartNum }
                         where ud.Key2 == "JobOper" &&
                               ud.ShortChar01 == idLigne &&
                               jo.Company == this.Session.CompanyID
                         select jm).FirstOrDefault();
            return parts == null ? "" : parts.MtlSeq.ToString();
        }

        private void GetListForJobNumFromProfile(string resId, string jobNum, string opCode)
        {
            DataTable dtLotProductionProfile = GetDataTable("LotProductionProfile");
            var lots = (from ud in this.Db.UD11.AsEnumerable()
                        where ud.Company == this.Session.CompanyID &&
                                ud.Key1 == "NoLot" &&
                                ud.Key2 == resId &&
                                ud.Key3 == jobNum &&
                                ud.ShortChar02 == opCode &&
                                ud.ShortChar03 != null &&
                                ud.ShortChar03 != ""
                        group ud by new
                        {
                            LotNum = ud.Character01,
                            Profile = ud.ShortChar01,
                            Ratio = ud.Number01
                        } into grp
                        select new
                        {
                            grp.Key.LotNum,
                            grp.Key.Profile,
                            grp.Key.Ratio,
                            IDLot = grp.Max(r => Convert.ToInt32(r.ShortChar03))
                        });
            foreach (var lot in lots)
            {
                dtLotProductionProfile.Rows.Add(lot.IDLot.ToString(), lot.LotNum, lot.Profile, lot.Ratio);
            }
            MergeDataTable(dtLotProductionProfile, true);
        }

        public DataSet GetListForJobNumFromProfile(string resId, string jobNum, string opCode, DataSet iDataSet)
        {
            LoadDataSet(iDataSet);
            GetListForJobNumFromProfile(resId, jobNum, opCode);
            return this.dsMiscAction;
        }

    }
}
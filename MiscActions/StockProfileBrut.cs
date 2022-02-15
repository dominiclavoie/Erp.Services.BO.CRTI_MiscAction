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
    class StockProfileBrut : BaseMiscAction, IMiscAction
    {

        public StockProfileBrut(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session) { }

        private string GetJobOperProfileBrut(string jobNum, int assemblySeq, int oprSeq)
        {
            var profileBrut = (from pm in this.Db.JobMtl
                               join pt in this.Db.Part on new { pm.Company, pm.PartNum }
                                                   equals new { pt.Company, pt.PartNum }
                               where pm.Company == this.Session.CompanyID &&
                                     pm.JobNum == jobNum &&
                                     pm.AssemblySeq == assemblySeq &&
                                     pm.RelatedOperation == oprSeq &&
                                     pt.ClassID == "BSm"
                               select pm).FirstOrDefault();
            string partNum = "";
            if (profileBrut != null)
            {
                partNum = profileBrut.PartNum;
                DataTable dtJobRefProfileBrut = GetDataTable("JobRefProfileBrut");
                dtJobRefProfileBrut.Rows.Add(profileBrut.MtlSeq);
                MergeDataTable(dtJobRefProfileBrut, true);
            }
            return partNum;
        }

        public override DataTable[] GetDataTable()
        {
            DataTable dt = new DataTable("StockProfileBrut");
            dt.Locale = CultureInfo.InvariantCulture;
            dt.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("PartNum", typeof(string)),
                new DataColumn("LotNum", typeof(string)),
                new DataColumn("Quantity", typeof(decimal)),
                new DataColumn("EstWIP", typeof(bool)),
                new DataColumn("IDLigne", typeof(string)),
                new DataColumn("InfoLot", typeof(string))
            });
            DataTable dt2 = new DataTable("ProfileBrut");
            dt2.Locale = CultureInfo.InvariantCulture;
            dt2.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("PartNum", typeof(string)),
                new DataColumn("Description", typeof(string))
            });
            DataTable dt3 = new DataTable("JobRefProfileBrut");
            dt3.Locale = CultureInfo.InvariantCulture;
            dt3.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("MtlSeq", typeof(int))
            });
            DataTable dt4 = new DataTable("LocalisationProfileBrut");
            dt4.Locale = CultureInfo.InvariantCulture;
            dt4.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("PartNum", typeof(string)),
                new DataColumn("LotNum", typeof(string)),
                new DataColumn("WarehouseCode", typeof(string)),
                new DataColumn("BinNum", typeof(string)),
                new DataColumn("IUM", typeof(string))
            });
            DataTable dt5 = new DataTable("ProfileDecoupeBrut");
            dt5.Locale = CultureInfo.InvariantCulture;
            dt5.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("PartNum", typeof(string)),
                new DataColumn("Description", typeof(string))
            });
            DataTable dt6 = new DataTable("ProfilePartFIFOCost");
            dt6.Locale = CultureInfo.InvariantCulture;
            dt6.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("PartNum", typeof(string)),
                new DataColumn("LotNum", typeof(string)),
                new DataColumn("FIFODate", typeof(DateTime)),
                new DataColumn("FIFOSeq", typeof(int)),
                new DataColumn("FIFOSubSeq", typeof(int)),
                new DataColumn("MaterialCost", typeof(decimal)),
                new DataColumn("LaborCost", typeof(decimal)),
                new DataColumn("BurdenCost", typeof(decimal)),
            });
            return new DataTable[] { dt, dt2, dt3, dt4, dt5, dt6 };
        }

        public void GetProfileBrutLocalisation(string partNum, string lotNum)
        {
            DataTable dtLocalisationProfileBrut = GetDataTable("LocalisationProfileBrut");
            if (!string.IsNullOrEmpty(partNum))
            {
                var partLots = (from pb in this.Db.PartBin
                                join pt in this.Db.Part on new { pb.Company, pb.PartNum }
                                                    equals new { pt.Company, pt.PartNum }
                                where pb.Company == this.Session.CompanyID &&
                                      pb.PartNum == partNum &&
                                      ((pt.TrackLots &&
                                      pb.LotNum == lotNum) ||
                                      !pt.TrackLots)
                                group pb by new
                                {
                                    pb.PartNum,
                                    pb.LotNum,
                                    pb.WarehouseCode,
                                    pb.BinNum,
                                    pt.IUM
                                } into grp
                                select new
                                {
                                    grp.Key.PartNum,
                                    grp.Key.LotNum,
                                    grp.Key.WarehouseCode,
                                    grp.Key.BinNum,
                                    grp.Key.IUM
                                });
                if (partLots.Any())
                {
                    foreach (var partLot in partLots)
                    {
                        dtLocalisationProfileBrut.Rows.Add(partLot.PartNum, partLot.LotNum, partLot.WarehouseCode, partLot.BinNum, partLot.IUM);
                    }
                }
                else
                {
                    var partLoc = (from pt in this.Db.Part
                                   join pp in this.Db.PartPlant on new { pt.Company, pt.PartNum }
                                                            equals new { pp.Company, pp.PartNum }
                                   join pw in this.Db.PlantWhse on new { pp.Company, pp.PartNum, pp.Plant, WarehouseCode = pp.PrimWhse }
                                                            equals new { pw.Company, pw.PartNum, pw.Plant, pw.WarehouseCode }
                                   where pt.Company == this.Session.CompanyID &&
                                         pt.PartNum == partNum
                                   select new
                                   {
                                       pt.PartNum,
                                       pw.WarehouseCode,
                                       BinNum = pw.PrimBin,
                                       pt.IUM
                                   }).FirstOrDefault();
                    if (partLoc != null)
                    {
                        dtLocalisationProfileBrut.Rows.Add(partLoc.PartNum, lotNum, partLoc.WarehouseCode, partLoc.BinNum, partLoc.IUM);
                    }
                }
            }
            MergeDataTable(dtLocalisationProfileBrut, true);
        }

        private IEnumerable<WIPPultrusion> GetWIPPultrusion(string partNum)
        {
            return (from wp in (from jp in this.Db.JobProd
                                join jph in this.Db.JobHead on new { jp.Company, jp.JobNum }
                                                        equals new { jph.Company, jph.JobNum }
                                join pt in this.Db.Part on new { jp.Company, jp.PartNum, ClassID = "BPSm" }
                                                    equals new { pt.Company, pt.PartNum, pt.ClassID }
                                join jh in this.Db.JobHead on new { jp.Company, JobNum = jp.TargetJobNum }
                                                       equals new { jh.Company, jh.JobNum }
                                join bl in this.Db.UD104 on new { jp.Company, RowID = jp.SysRowID.ToString(), Type = "JobOper" }
                                                       equals new { bl.Company, RowID = bl.Character10, Type = bl.Key2 }
                                join pr in this.Db.UD105A on new { bl.Company, IDLigne = bl.ShortChar01, Type = "Production" }
                                                       equals new { pr.Company, IDLigne = pr.ChildKey3, Type = pr.ChildKey1 }
                                where jp.Company == this.Session.CompanyID &&
                                      jh.PartNum == partNum &&
                                      jh.JobClosed == false &&
                                      jh.JobEngineered == true &&
                                      jh.JobReleased == true &&
                                      jph.JobClosed == false &&
                                      jph.JobEngineered == true &&
                                      jph.JobReleased == true
                                group new { jh, pr } by new
                                {
                                    jh.PartNum,
                                    LotNum = pr.Character05,
                                    IDLigne = pr.ChildKey3,
                                    jh.JobNum,
                                    BarLength = pr.Number01
                                } into prd
                                select new
                                {
                                    prd.Key.PartNum,
                                    prd.Key.LotNum,
                                    prd.Key.IDLigne,
                                    prd.Key.JobNum,
                                    prd.Key.BarLength,
                                    Quantity = prd.Sum(r => r.pr.Number05)
                                })
                    join rs in (from rs in this.Db.UD104
                                where rs.Company == this.Session.CompanyID &&
                                      rs.Key2 == "Reserve" &&
                                      rs.Character01 == partNum
                                group rs by new
                                {
                                    PartNum = rs.Character01,
                                    LotNum = rs.Character02,
                                    IDLigne = rs.Key1
                                } into rsv
                                select new
                                {
                                    rsv.Key.PartNum,
                                    rsv.Key.LotNum,
                                    rsv.Key.IDLigne,
                                    Quantity = rsv.Sum(r => r.Number03)
                                }) on new { wp.PartNum, wp.LotNum, wp.IDLigne }
                               equals new { rs.PartNum, rs.LotNum, rs.IDLigne } into rsv
                    from rs in rsv.DefaultIfEmpty()
                    join rc in (from rc in this.Db.PartTran
                                where rc.Company == this.Session.CompanyID &&
                                      rc.TranType == "MFG-STK"
                                group rc by new
                                {
                                    rc.PartNum,
                                    rc.LotNum,
                                    rc.JobNum
                                } into rcp
                                select new
                                {
                                    rcp.Key.PartNum,
                                    rcp.Key.LotNum,
                                    rcp.Key.JobNum,
                                    Quantity = rcp.Sum(r => r.TranQty)
                                }) on new { wp.PartNum, wp.LotNum, wp.JobNum }
                               equals new { rc.PartNum, rc.LotNum, rc.JobNum } into rcp
                    from rc in rcp.DefaultIfEmpty()
                    select new WIPPultrusion
                    {
                        PartNum = wp.PartNum,
                        LotNum = wp.LotNum,
                        IDLigne = wp.IDLigne,
                        Quantity = wp.Quantity - (rs == null ? 0m : rs.Quantity) - (rc == null ? 0m : rc.Quantity)
                    }).Where(tt => tt.Quantity > 0m);
        }

        public bool CheckWIPQuantityAvailable(string partNum, string lotNum, string idLigne, decimal quantity)
        {
            return GetWIPPultrusion(partNum).Where(tt => tt.PartNum == partNum && tt.LotNum == lotNum && tt.IDLigne == idLigne).Sum(tt => tt.Quantity) >= quantity;
        }

        private void AddWIPQuantities(string partNum, ref DataTable dtStockProfileBrut)
        {
            foreach (WIPPultrusion wip in GetWIPPultrusion(partNum))
            {
                string infoLot = string.Join("~", new string[] { wip.PartNum, wip.LotNum, "1", wip.IDLigne });
                dtStockProfileBrut.Rows.Add(wip.PartNum, wip.LotNum, wip.Quantity, true, wip.IDLigne, infoLot);
            }
        }

        private class WIPPultrusion
        {
            private string _partNum;
            private string _lotNum;
            private string _idLigne;
            private decimal _quantity;
            public string PartNum { get => _partNum; set => _partNum = value; }
            public string LotNum { get => _lotNum; set => _lotNum = value; }
            public string IDLigne { get => _idLigne; set => _idLigne = value; }
            public decimal Quantity { get => _quantity; set => _quantity = value; }
            public WIPPultrusion() { }
        }

        private void GetOnHandStock(string partNum, ref DataTable dtStockProfileBrut)
        {
            var partLots = (from pb in this.Db.PartBin
                            where pb.Company == this.Session.CompanyID &&
                                  pb.PartNum == partNum &&
                                  pb.LotNum != ""
                            group pb by new
                            {
                                pb.PartNum,
                                pb.LotNum,
                            } into grp
                            select new
                            {
                                grp.Key.PartNum,
                                grp.Key.LotNum,
                                Quantity = grp.Sum(r => r.OnhandQty)
                            });
            foreach (var partLot in partLots)
            {
                string infoLot = string.Join("~", new string[] { partLot.PartNum, partLot.LotNum, "0", "" });
                dtStockProfileBrut.Rows.Add(partLot.PartNum, partLot.LotNum, partLot.Quantity, false, "", infoLot);
            }
        }

        private void GetStockProfileBrut(string partNum)
        {
            DataTable dtStockProfileBrut = GetDataTable("StockProfileBrut");
            if (!string.IsNullOrEmpty(partNum))
            {
                GetOnHandStock(partNum, ref dtStockProfileBrut);
                AddWIPQuantities(partNum, ref dtStockProfileBrut);
                if (dtStockProfileBrut.Rows.Count == 0)
                {
                    dtStockProfileBrut.Rows.Add(partNum, "", 0m, true, "");
                }
            }
            MergeDataTable(dtStockProfileBrut, true);
        }
        
        private void GetSimilarProfileBrut(string partNum)
        {
            DataTable dtProfileBrut = GetDataTable("ProfileBrut");
            string[] substring = partNum.Split('-');
            if (!string.IsNullOrEmpty(partNum) && substring.Length == 2)
            {
                string partContains = string.Format("{0}-", substring[0]);
                var parts = (from pt in this.Db.Part
                             where pt.Company == this.Session.CompanyID &&
                                   pt.PartNum.StartsWith(partContains) &&
                                   pt.TrackLots == true &&
                                   pt.InActive == false &&
                                   pt.OnHold == false
                             orderby pt.PartNum
                             select pt);
                foreach (var part in parts)
                {
                    dtProfileBrut.Rows.Add(part.PartNum, part.PartDescription);
                }
            }
            MergeDataTable(dtProfileBrut, true);
        }

        private void GetProfileDecoupeBrut()
        {
            DataTable dtProfileBrut = GetDataTable("ProfileDecoupeBrut");
            string partContains = string.Format("{0}-", "lower");
            IEnumerable<ProfileBrut> parts = (from pt in this.Db.Part
                                              where pt.Company == this.Session.CompanyID &&
                                                  (pt.PartNum.StartsWith("lowerg1-") || 
                                                   pt.PartNum.StartsWith("lowerg2-") ||
                                                   pt.PartNum.StartsWith("lowerg3-") ||
                                                   pt.PartNum.StartsWith("lowerg4-") ||
                                                   pt.PartNum.StartsWith("upperg1-") ||
                                                   pt.PartNum.StartsWith("upperg2-") ||
                                                   pt.PartNum.StartsWith("upperg3-") ||
                                                   pt.PartNum.StartsWith("upperg4-")) &&
                                                  pt.TrackLots == true &&
                                                  pt.InActive == false &&
                                                  pt.OnHold == false
                                              select new ProfileBrut
                                              {
                                                  PartNum = pt.PartNum,
                                                  PartDescription = pt.PartDescription
                                              });
            parts = parts.OrderBy(tt => tt.Order1).ThenBy(tt => tt.Order2);
            foreach (var part in parts)
            {
                dtProfileBrut.Rows.Add(part.PartNum, part.PartDescription);
            }
            MergeDataTable(dtProfileBrut, true);
        }

        public class ProfileBrut
        {
            private string _partNum;
            private string _partDescription;
            public string PartNum { get => _partNum; set => _partNum = value; }
            public string PartDescription { get => _partDescription; set => _partDescription = value; }
            public string Order1 { get => _partNum.Split('-')[0]; }
            public decimal Order2 { get => decimal.Parse(_partNum.Split('-')[1]); }
            public ProfileBrut() { }

        }

        private void GetPartFIFOCost(string partNum, string lotNum, string partTranPKs)
        {
            DataTable dtProfilePartFIFOCost = GetDataTable("ProfilePartFIFOCost");
            string[] partTranPKsSplit = partTranPKs.Split('~');
            bool flag = true;
            DateTime tranDate = DateTime.Now;
            int tranTime = 0;
            int tranNum = 0;
            if(partTranPKsSplit.Length == 3)
            {
                if(!DateTime.TryParseExact(partTranPKsSplit[0], "MM/dd/yy", null, System.Globalization.DateTimeStyles.None, out tranDate))
                {
                    flag = false;
                }
                if(!int.TryParse(partTranPKsSplit[1], out tranTime))
                {
                    flag = false;
                }
                if (!int.TryParse(partTranPKsSplit[2], out tranNum))
                {
                    flag = false;
                }
            }
            if(flag)
            {
                decimal materialCost = 0m;
                decimal laborCost = 0m;
                decimal burdenCost = 0m;
                GetPartCost(partNum, out materialCost, out laborCost, out burdenCost);
                var partCost = (from pc in this.Db.PartFIFOCost
                                where pc.Company == this.Session.CompanyID &&
                                      pc.PartNum == partNum &&
                                      pc.LotNum == lotNum &&
                                      pc.SourceSysDate == tranDate &&
                                      pc.SourceSysTime == tranTime &&
                                      pc.SourceTranNum == tranNum &&
                                      pc.InActive == false
                                select pc).FirstOrDefault();
                if(partCost != null)
                {
                    DateTime fifoDate = partCost.FIFODate ?? DateTime.Now;
                    dtProfilePartFIFOCost.Rows.Add(partCost.PartNum, partCost.LotNum, fifoDate, partCost.FIFOSeq, partCost.FIFOSubSeq, materialCost, laborCost, burdenCost);
                }
            }
            MergeDataTable(dtProfilePartFIFOCost, true);
        }

        private void GetPartCost(string partNum, out decimal materialCost, out decimal laborCost, out decimal burdenCost)
        {
            materialCost = 0m;
            laborCost = 0m;
            burdenCost = 0m;
            string[] pnComp = partNum.Split('-');
            decimal materialUnitCost = 0m;
            decimal laborUnitCost = 0m;
            decimal burdenUnitCost = 0m;
            switch (pnComp[0])
            {
                case "upperg1":
                    materialUnitCost = 4.579m;
                    laborUnitCost = 0.831m;
                    burdenUnitCost = 1.511m;
                    break;
                case "upperg2":
                    materialUnitCost = 5.371m;
                    laborUnitCost = 0.826m;
                    burdenUnitCost = 1.772m;
                    break;
                case "upperg3":
                    materialUnitCost = 5.689m;
                    laborUnitCost = 0.806m;
                    burdenUnitCost = 1.877m;
                    break;
                case "upperg4":
                    materialUnitCost = 5.412m;
                    laborUnitCost = 0.887m;
                    burdenUnitCost = 1.786m;
                    break;
                case "lowerg1":
                    materialUnitCost = 3.200m;
                    laborUnitCost = 0.887m;
                    burdenUnitCost = 1.056m;
                    break;
                case "lowerg2":
                    materialUnitCost = 4.481m;
                    laborUnitCost = 1.118m;
                    burdenUnitCost = 1.479m;
                    break;
                case "lowerg3":
                    materialUnitCost = 4.481m;
                    laborUnitCost = 1.118m;
                    burdenUnitCost = 1.479m;
                    break;
                case "lowerg4":
                    materialUnitCost = 6.912m;
                    laborUnitCost = 0.950m;
                    burdenUnitCost = 2.281m;
                    break;
            }
            decimal length = decimal.Parse(pnComp[1]) / 1000m;
            materialCost = materialUnitCost * length;
            laborCost = laborUnitCost * length;
            burdenCost = burdenUnitCost * length;
        }

        public DataSet GetJobOperStockProfileBrut(string jobNum, int assemblySeq, int oprSeq, DataSet iDataSet)
        {
            LoadDataSet(iDataSet);
            string partNum = GetJobOperProfileBrut(jobNum, assemblySeq, oprSeq);
            GetSimilarProfileBrut(partNum);
            GetStockProfileBrut(partNum);
            return this.dsMiscAction;
        }

        public DataSet GetStockProfileBrut(string partNum, DataSet iDataSet)
        {
            LoadDataSet(iDataSet);
            GetStockProfileBrut(partNum);
            return this.dsMiscAction;
        }

        public DataSet GetProfileDecoupeBrut(DataSet iDataSet)
        {
            LoadDataSet(iDataSet);
            GetProfileDecoupeBrut();
            return this.dsMiscAction;
        }

        public DataSet GetPartFIFOCost(string partNum, string lotNum, string partTranPKs, DataSet iDataSet)
        {
            LoadDataSet(iDataSet);
            GetPartFIFOCost(partNum, lotNum, partTranPKs);
            return this.dsMiscAction;
        }

        public DataSet GetProfileBrutLocalisation(string partNum, string lotNum, DataSet iDataSet)
        {
            LoadDataSet(iDataSet);
            GetProfileBrutLocalisation(partNum, lotNum);
            return this.dsMiscAction;
        }

    }
}

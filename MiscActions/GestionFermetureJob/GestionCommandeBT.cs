using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Globalization;
using Erp.Tables;
using Erp.Tablesets;
using Erp.Services.BO;
using Epicor.Data;
using Ice;
using Ice.Tables;
using Ice.Tablesets;
using Erp.BO.CRTI_MiscAction.MiscActions.GestionFermetureJob;

namespace Erp.BO.CRTI_MiscAction
{
    class GestionCommandeBT : BaseMiscAction, IMiscAction
    {
        public GestionCommandeBT(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session) {}

        public override DataTable[] GetDataTable()
        {
            DataTable dt = new DataTable("CommandeBTOuvert");
            dt.Locale = CultureInfo.InvariantCulture;
            dt.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("OrderNum", typeof(int)),
                new DataColumn("Customer", typeof(string)),
                new DataColumn("ReqDate", typeof(DateTime)),
                new DataColumn("SysRowID", typeof(Guid))
            });
            DataTable dt2 = new DataTable("CommandeDetailBTOuvert");
            dt2.Locale = CultureInfo.InvariantCulture;
            dt2.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("Select", typeof(bool)),
                new DataColumn("OrderNum", typeof(int)),
                new DataColumn("OrderLine", typeof(int)),
                new DataColumn("OrderRelNum", typeof(int)),
                new DataColumn("Customer", typeof(string)),
                new DataColumn("ReqDate", typeof(DateTime)),
                new DataColumn("JobNum", typeof(string)),
                new DataColumn("PartNum", typeof(string)),
                new DataColumn("OpCode", typeof(string)),
                new DataColumn("RequiredQty", typeof(decimal)),
                new DataColumn("RemainingQty", typeof(decimal)),
                new DataColumn("IsFinalOper", typeof(bool)),
                new DataColumn("ProdDetail", typeof(string)),
                new DataColumn("SysRowID", typeof(Guid))
            });
            DataTable dt3 = new DataTable("InventaireBTOuvert");
            dt3.Locale = CultureInfo.InvariantCulture;
            dt3.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("Select", typeof(bool)),
                new DataColumn("ReqDate", typeof(DateTime)),
                new DataColumn("JobNum", typeof(string)),
                new DataColumn("PartNum", typeof(string)),
                new DataColumn("OpCode", typeof(string)),
                new DataColumn("RequiredQty", typeof(decimal)),
                new DataColumn("RemainingQty", typeof(decimal)),
                new DataColumn("IsFinalOper", typeof(bool)),
                new DataColumn("ProdDetail", typeof(string)),
                new DataColumn("SysRowID", typeof(Guid))
            });
            DataTable dt4 = new DataTable("JobToJobBTOuvert");
            dt4.Locale = CultureInfo.InvariantCulture;
            dt4.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("Select", typeof(bool)),
                new DataColumn("ReqDate", typeof(DateTime)),
                new DataColumn("JobNum", typeof(string)),
                new DataColumn("JobNum2", typeof(string)),
                new DataColumn("AssemblySeq2", typeof(int)),
                new DataColumn("JobSeq2", typeof(int)),
                new DataColumn("PartNum", typeof(string)),
                new DataColumn("OpCode", typeof(string)),
                new DataColumn("RequiredQty", typeof(decimal)),
                new DataColumn("RemainingQty", typeof(decimal)),
                new DataColumn("IsFinalOper", typeof(bool)),
                new DataColumn("ProdDetail", typeof(string)),
                new DataColumn("SysRowID", typeof(Guid))
            });
            return new DataTable[] { dt, dt2, dt3, dt4 };
        }

        private bool HasWIPReservation(string jobNum, out string message)
        {
            message = string.Empty;
            var rsv = (from ud in this.Db.UD104
                       where ud.Company == this.Session.CompanyID &&
                             ud.Key2 == "Reserve" &&
                             ud.ShortChar01 == jobNum &&
                             ud.Number03 > 0m
                       select ud);
            if (rsv.Any())
            {
                List<string> jobs = (from jb in rsv.AsEnumerable()
                                    group jb by new
                                    {
                                        jb.ShortChar02
                                    } into jb
                                    select new
                                    {
                                        JobNum = jb.Key.ShortChar02
                                    }).Select(tt => tt.JobNum).ToList();
                message = string.Format("Le bon de travail contient des pièces consommées provenant d'autre(s) bon(s) de travail. Veuillez d'abord le(s) fermer, voici le(s) numéro(s): {0}", string.Join(", ", jobs));
                return true;
            }
            return false;
        }

        private class JobBatch
        {
            public DateTime JobBatchDate { get; set; }
            public string Shift { get; set; }
            public JobBatch() { }
            public override string ToString()
            {
                return string.Format("Date: {0}, Quart de travail: {1}", JobBatchDate.ToShortDateString(), Shift);
            }
        }

        private bool HasUnApprovedLabor(string jobNum, out string message)
        {
            message = string.Empty;
            var lbr = (from lb in this.Db.LaborDtl.AsEnumerable()
                       join jb in this.Db.UD105.AsEnumerable() on new { lb.Company, RefJobBatch = lb.UDField("UD_RefJobBatch_c", false).ToString() }
                                                           equals new { jb.Company, RefJobBatch = string.Join("-", new string[] { jb.Key1, jb.Key2 }) }
                       where lb.Company == this.Session.CompanyID &&
                             lb.JobNum == jobNum &&
                             lb.TimeStatus == "E"
                       select new
                       {
                           JobBatchDate = jb.Date01 ?? DateTime.Now,
                           Shift = jb.Character03
                       });
            if (lbr.Any())
            {
                List<string> jobBatch = (from jb in lbr.AsEnumerable()
                                         group jb by new
                                         {
                                             jb.JobBatchDate,
                                             jb.Shift
                                         } into jb
                                         select new JobBatch
                                         {
                                             JobBatchDate = jb.Key.JobBatchDate,
                                             Shift = jb.Key.Shift
                                         }).Select(tt => tt.ToString()).ToList();
                message = string.Format("Des entrées de production de ce bon de travail n'ont pas été validés. Veuillez valider les dates suivantes: \r\n{0}", string.Join("\r\n", jobBatch));
                return true;
            }
            return false;
        }

        private bool HasOpenLinkedJobToJob(string jobNum, out string message)
        {
            message = string.Empty;
            var lnk = (from jp in this.Db.JobProd
                       join jh in this.Db.JobHead on new { jp.Company, jp.JobNum }
                                              equals new { jh.Company, jh.JobNum }
                       where jp.Company == this.Session.CompanyID &&
                             jp.TargetJobNum == jobNum &&
                             jh.JobClosed == false
                       select new
                       {
                           jh.JobNum
                       });
            if (lnk.Any())
            {
                List<string> jobs = (from jb in lnk.AsEnumerable()
                                     group jb by new
                                     {
                                         jb.JobNum
                                     } into jb
                                     select new
                                     {
                                         JobNum = jb.Key.JobNum
                                     }).Select(tt => tt.JobNum).ToList();
                message = string.Format("Le bon de travail contient des bons de travail liés qui ne sont pas fermés. Veuillez d'abord le(s) fermer, voici le(s) numéro(s): {0}", string.Join(", ", jobs));
                return true;
            }
            return false;
        }

        public bool ValidateJobCanClose(string jobNum, out string message)
        {
            message = string.Empty;
            if(HasWIPReservation(jobNum, out message))
            {
                return false;
            }
            
            if(HasUnApprovedLabor(jobNum, out message))
            {
                return false;
            }
            
            if(HasOpenLinkedJobToJob(jobNum, out message))
            {
                return false;
            }

            return true;
        }

        public void CommandePossedeBT(int orderNum, int orderLine, int orderRelNum, out string jobNum, out string wipJobs)
        {
            jobNum = string.Empty;
            wipJobs = string.Empty;
            var job = (from jp in Db.JobProd
                       join jh in Db.JobHead on new { jp.Company, jp.JobNum }
                                         equals new { jh.Company, jh.JobNum }
                       where jp.Company == this.Session.CompanyID &&
                             jp.OrderNum == orderNum &&
                             jp.OrderLine == orderLine &&
                             jp.OrderRelNum == orderRelNum &&
                             jh.JobClosed == false &&
                             jh.JobFirm == true
                       select jp);
            string[] jobNums = job.Select(tt => tt.JobNum).ToArray();
            jobNum = string.Join("|", jobNums);

            string[] wips = (from lb in Db.LaborDtl
                             where lb.Company == this.Session.CompanyID &&
                                   jobNums.Contains(lb.JobNum)
                             group lb by new
                             {
                                 lb.JobNum
                             } into lb
                             select lb.Key.JobNum).ToArray();
            wipJobs = string.Join("|", wips);
        }

        private class FinalJobOper
        {
            public string Company { get; set; }
            public string JobNum { get; set; }
            public int AssemblySeq { get; set; }
            public int OprSeq { get; set; }
            public FinalJobOper() { }
        }

        private IEnumerable<FinalJobOper> GetFinalJobOpers()
        {
            return (from jh in Db.JobHead
                    join ja in Db.JobAsmbl on new { jh.Company, jh.JobNum }
                                       equals new { ja.Company, ja.JobNum }
                    join jo in Db.JobOper on new { ja.Company, ja.JobNum, ja.AssemblySeq }
                                      equals new { jo.Company, jo.JobNum, jo.AssemblySeq }
                    where jh.Company == this.Session.CompanyID &&
                        !jh.JobClosed &&
                        jh.JobEngineered &&
                        jh.JobFirm &&
                        jh.JobReleased
                    group new { ja, jo } by new
                    {
                        ja.Company,
                        ja.JobNum,
                        ja.AssemblySeq,
                        ja.FinalOpr
                    } into grp
                    select new FinalJobOper
                    {
                        Company = grp.Key.Company,
                        JobNum = grp.Key.JobNum,
                        AssemblySeq = grp.Key.AssemblySeq,
                        OprSeq = grp.Key.FinalOpr == 0 ? grp.Max(r => r.jo.OprSeq) : grp.Key.FinalOpr
                    });
        }

        private class ProdDetail
        {
            public string LotNum { get; set; }
            public decimal BarLength { get; set; }
            public decimal Quantity { get; set; }
            public ProdDetail() { }
            public override string ToString()
            {
                return string.Join("~", new string[] { LotNum, BarLength.ToString(), Quantity.ToString() });
            }
        }

        private class OperProdDetail
        {
            public string Company { get; set; }
            public string JobNum { get; set; }
            public Guid JPRowID { get; set; }
            public Guid JORowID { get; set; }
            public List<ProdDetail> ProdDetail { get; set; }
            public string ProdDetailStr
            {
                get { return ProdDetail.Any() ? string.Join("|", ProdDetail.Select(x => x.ToString()).ToArray()) : string.Empty; }
            }

            public OperProdDetail() { }
        }

        private IEnumerable<OperProdDetail> GetOperationProductionDetail()
        {
            var prds = (from prd in (from jh in this.Db.JobHead.AsEnumerable()
                    join jp in this.Db.JobProd.AsEnumerable() on new { jh.Company, jh.JobNum }
                                                equals new { jp.Company, jp.JobNum }
                    join ja in Db.JobAsmbl.AsEnumerable() on new { jh.Company, jh.JobNum }
                                        equals new { ja.Company, ja.JobNum }
                    join jo in Db.JobOper.AsEnumerable() on new { ja.Company, ja.JobNum, ja.AssemblySeq }
                                        equals new { jo.Company, jo.JobNum, jo.AssemblySeq }
                    join jf in GetFinalJobOpers().AsEnumerable() on new { jo.Company, jo.JobNum, jo.AssemblySeq, jo.OprSeq }
                                                equals new { jf.Company, jf.JobNum, jf.AssemblySeq, jf.OprSeq }
                    join bl in this.Db.UD104.AsEnumerable() on new { jo.Company, RowID = jo.SysRowID.ToString(), JPRowID = jp.SysRowID.ToString(), Type = "JobOper" }
                                            equals new { bl.Company, RowID = bl.Character09, JPRowID = bl.Character10, Type = bl.Key2 }
                    join pr in this.Db.UD105A.AsEnumerable() on new { bl.Company, IDLigne = bl.ShortChar01, Type = "Production" }
                                            equals new { pr.Company, IDLigne = pr.ChildKey3, Type = pr.ChildKey1 }
                    where jh.Company == this.Session.CompanyID &&
                        jh.JobClosed == false &&
                        jh.JobEngineered == true &&
                        jh.JobReleased == true
                    group new { jp, jo, pr } by new
                    {
                        jp.Company,
                        jp.JobNum,
                        JPRowID = jp.SysRowID,
                        JORowID = jo.SysRowID,
                        LotNum = pr.Character05,
                        BarLength = pr.Number01
                    } into prd
                    select new
                    {
                        prd.Key.Company,
                        prd.Key.JobNum,
                        prd.Key.JPRowID,
                        prd.Key.JORowID,
                        prd.Key.LotNum,
                        prd.Key.BarLength,
                        Quantity = prd.Sum(r => r.pr.Number05)
                    }).AsEnumerable()
                    group prd by new
                    {
                        prd.Company,
                        prd.JobNum,
                        prd.JPRowID,
                        prd.JORowID
                    } into prd
                    select new
                    {
                        Company = prd.Key.Company,
                        JobNum = prd.Key.JobNum,
                        JPRowID = prd.Key.JPRowID,
                        JORowID = prd.Key.JORowID,
                        ProdDetail = prd.Select(tt => new ProdDetail
                        {
                            LotNum = tt.LotNum,
                            BarLength = tt.BarLength,
                            Quantity = tt.Quantity
                        }).ToList()
                    }).AsEnumerable();
                        return prds.Select(prd => new OperProdDetail
                        {
                            Company = prd.Company,
                            JobNum = prd.JobNum,
                            JPRowID = prd.JPRowID,
                            JORowID = prd.JORowID,
                            ProdDetail = prd.ProdDetail
                        }).AsEnumerable();
        }

        private OpenJobOrderCollection GetOpenJobOrders()
        {
            IEnumerable<OperProdDetail> operProdDetails = GetOperationProductionDetail();
            OpenJobOrderCollection jobs = new OpenJobOrderCollection();
            jobs.openJobs = (from oj in (
                                 from jh in this.Db.JobHead
                                 join jp in this.Db.JobProd on new { jh.Company, jh.JobNum }
                                                        equals new { jp.Company, jp.JobNum }
                                 join or in this.Db.OrderRel on new { jp.Company, jp.OrderNum, jp.OrderLine, jp.OrderRelNum }
                                                         equals new { or.Company, or.OrderNum, or.OrderLine, or.OrderRelNum }
                                 join ol in this.Db.OrderDtl on new { or.Company, or.OrderNum, or.OrderLine }
                                                         equals new { ol.Company, ol.OrderNum, ol.OrderLine }
                                 join oh in this.Db.OrderHed on new { ol.Company, ol.OrderNum }
                                                         equals new { oh.Company, oh.OrderNum }
                                 join cu in this.Db.Customer on new { oh.Company, oh.CustNum }
                                                         equals new { cu.Company, cu.CustNum }
                                 join bl in this.Db.UD104 on new { jp.Company, RowID = jp.SysRowID.ToString(), Type = "JobOper" }
                                                      equals new { bl.Company, RowID = bl.Character10, Type = bl.Key2 }
                                 join jo in this.Db.JobOper on new { bl.Company, RowID = bl.Character09, Type = bl.Key2 }
                                                        equals new { jo.Company, RowID = jo.SysRowID.ToString(), Type = "JobOper" }
                                 join jf in GetFinalJobOpers() on new { jo.Company, jo.JobNum, jo.AssemblySeq }
                                                           equals new { jf.Company, jf.JobNum, jf.AssemblySeq }
                                 where jh.Company == this.Session.CompanyID &&
                                       !jh.JobClosed &&
                                       jh.JobEngineered &&
                                       jh.JobFirm &&
                                       jh.JobReleased
                                 select new
                                 {
                                     jh.Company,
                                     OrderNum = oh.OrderNum,
                                     OrderLine = ol.OrderLine,
                                     OrderRelNum = or.OrderRelNum,
                                     CustomerName = cu.Name,
                                     ReqDate = or.ReqDate ?? DateTime.Now,
                                     JobNum = jh.JobNum,
                                     PartNum = jh.PartNum,
                                     OpCode = jo.OpCode,
                                     RequiredQty = jp.ProdQty,
                                     RemainingQty = bl.Number20,
                                     IsFinalOper = jo.OprSeq == jf.OprSeq,
                                     JPRowID = jp.SysRowID,
                                     JORowID = jo.SysRowID
                                 }
                             ).AsEnumerable()
                             join pr in operProdDetails.AsEnumerable() on new { oj.Company, oj.JobNum, oj.JPRowID, oj.JORowID }
                                                                   equals new { pr.Company, pr.JobNum, pr.JPRowID, pr.JORowID } into prd
                             from pr in prd.DefaultIfEmpty()
                             select new OpenJobOrder
                             {
                                 OrderNum = oj.OrderNum,
                                 OrderLine = oj.OrderLine,
                                 OrderRelNum = oj.OrderRelNum,
                                 CustomerName = oj.CustomerName,
                                 ReqDate = oj.ReqDate,
                                 JobNum = oj.JobNum,
                                 PartNum = oj.PartNum,
                                 OpCode = oj.OpCode,
                                 RequiredQty = oj.RequiredQty,
                                 RemainingQty = oj.RemainingQty,
                                 IsFinalOper = oj.IsFinalOper,
                                 ProdDetail = pr == null ? string.Empty : pr.ProdDetailStr
                             });
            return jobs;
        }

        private OpenJobInventoryCollection GetOpenJobsInventory()
        {
            IEnumerable<OperProdDetail> operProdDetails = GetOperationProductionDetail();
            OpenJobInventoryCollection jobs = new OpenJobInventoryCollection();
            jobs.openJobs = (from jh in this.Db.JobHead.AsEnumerable()
                             join jp in this.Db.JobProd.AsEnumerable() on new { jh.Company, jh.JobNum }
                                                    equals new { jp.Company, jp.JobNum }
                             join bl in this.Db.UD104.AsEnumerable() on new { jp.Company, RowID = jp.SysRowID.ToString(), Type = "JobOper" }
                                                  equals new { bl.Company, RowID = bl.Character10, Type = bl.Key2 }
                             join jo in this.Db.JobOper.AsEnumerable() on new { bl.Company, RowID = bl.Character09, Type = bl.Key2 }
                                                    equals new { jo.Company, RowID = jo.SysRowID.ToString(), Type = "JobOper" }
                             join jf in GetFinalJobOpers().AsEnumerable() on new { jo.Company, jo.JobNum, jo.AssemblySeq }
                                                       equals new { jf.Company, jf.JobNum, jf.AssemblySeq }
                             join pr in operProdDetails.AsEnumerable() on new { jp.Company, jp.JobNum, JPRowID = jp.SysRowID, JORowID = jo.SysRowID }
                                                                   equals new { pr.Company, pr.JobNum, pr.JPRowID, pr.JORowID } into prd
                             from pr in prd.DefaultIfEmpty()
                             where jh.Company == this.Session.CompanyID &&
                                   !jh.JobClosed &&
                                   jh.JobEngineered &&
                                   jh.JobFirm &&
                                   jh.JobReleased &&
                                   !string.IsNullOrEmpty(jp.WarehouseCode)
                             select new OpenJobInventory
                             {
                                 ReqDate = jh.ReqDueDate ?? DateTime.Now,
                                 JobNum = jh.JobNum,
                                 PartNum = jh.PartNum,
                                 OpCode = jo.OpCode,
                                 RequiredQty = jp.ProdQty,
                                 RemainingQty = bl.Number20,
                                 IsFinalOper = jo.OprSeq == jf.OprSeq,
                                 ProdDetail = pr == null ? string.Empty : pr.ProdDetailStr
                             });
            return jobs;
        }

        private OpenJobToJobCollection GetOpenJobsToJob()
        {
            IEnumerable<OperProdDetail> operProdDetails = GetOperationProductionDetail();
            OpenJobToJobCollection jobs = new OpenJobToJobCollection();
            jobs.openJobs = (from jh in this.Db.JobHead.AsEnumerable()
                             join jp in this.Db.JobProd.AsEnumerable() on new { jh.Company, jh.JobNum }
                                                    equals new { jp.Company, jp.JobNum }
                             join bl in this.Db.UD104.AsEnumerable() on new { jp.Company, RowID = jp.SysRowID.ToString(), Type = "JobOper" }
                                                  equals new { bl.Company, RowID = bl.Character10, Type = bl.Key2 }
                             join jo in this.Db.JobOper.AsEnumerable() on new { bl.Company, RowID = bl.Character09, Type = bl.Key2 }
                                                    equals new { jo.Company, RowID = jo.SysRowID.ToString(), Type = "JobOper" }
                             join jf in GetFinalJobOpers().AsEnumerable() on new { jo.Company, jo.JobNum, jo.AssemblySeq }
                                                       equals new { jf.Company, jf.JobNum, jf.AssemblySeq }
                             join pr in operProdDetails.AsEnumerable() on new { jp.Company, jp.JobNum, JPRowID = jp.SysRowID, JORowID = jo.SysRowID }
                                                                   equals new { pr.Company, pr.JobNum, pr.JPRowID, pr.JORowID } into prd
                             from pr in prd.DefaultIfEmpty()
                             where jh.Company == this.Session.CompanyID &&
                                   !jh.JobClosed &&
                                   jh.JobEngineered &&
                                   jh.JobFirm &&
                                   jh.JobReleased &&
                                   !string.IsNullOrEmpty(jp.TargetJobNum)
                             select new OpenJobToJob
                             {
                                 ReqDate = jh.ReqDueDate ?? DateTime.Now,
                                 JobNum = jh.JobNum,
                                 JobNum2 = jp.TargetJobNum,
                                 AssemblySeq2 = jp.TargetAssemblySeq,
                                 JobSeq2 = jp.TargetMtlSeq,
                                 PartNum = jh.PartNum,
                                 OpCode = jo.OpCode,
                                 RequiredQty = jp.ProdQty,
                                 RemainingQty = bl.Number20,
                                 IsFinalOper = jo.OprSeq == jf.OprSeq,
                                 ProdDetail = pr == null ? string.Empty : pr.ProdDetailStr
                             });
            return jobs;
        }

        private void LoadOpenJobsInventory()
        {
            DataTable dtInventaireBTOuvert = GetDataTable("InventaireBTOuvert");
            OpenJobInventoryCollection jobs = GetOpenJobsInventory();
            foreach (var job in jobs.openJobs)
            {
                dtInventaireBTOuvert.Rows.Add(job.GetValues());
            }
            MergeDataTable(dtInventaireBTOuvert, true);
        }

        private void LoadOpenJobsToJob()
        {
            DataTable dtJobToJobBTOuvert = GetDataTable("JobToJobBTOuvert");
            OpenJobToJobCollection jobs = GetOpenJobsToJob();
            foreach (var job in jobs.openJobs)
            {
                dtJobToJobBTOuvert.Rows.Add(job.GetValues());
            }
            MergeDataTable(dtJobToJobBTOuvert, true);
        }

        private void LoadOpenJobOrders()
        {
            DataTable dtCommandeBTOuvert = GetDataTable("CommandeBTOuvert");
            OpenJobOrderCollection jobs = GetOpenJobOrders();
            IEnumerable<OpenJobOrderGroup> orders = jobs.GroupByOrder();
            foreach (var order in orders)
            {
                dtCommandeBTOuvert.Rows.Add(order.OrderNum, order.CustomerName, order.ReqDate, Guid.NewGuid());
            }
            MergeDataTable(dtCommandeBTOuvert, true);
        }

        private void LoadOrderDetail(int orderNum)
        {
            DataTable dtCommandeDetailBTOuvert = GetDataTable("CommandeDetailBTOuvert");
            OpenJobOrderCollection jobs = GetOpenJobOrders();
            IEnumerable<OpenJobOrder> orderDtl = jobs.GetOrderDetail(orderNum);
            foreach (var order in orderDtl)
            {
                dtCommandeDetailBTOuvert.Rows.Add(order.GetValues());
            }
            MergeDataTable(dtCommandeDetailBTOuvert, true);
        }

        public DataSet GetOpenJobsInventory(DataSet iDataSet)
        {
            LoadDataSet(iDataSet);
            LoadOpenJobsInventory();
            return this.dsMiscAction;
        }

        public DataSet GetOpenJobsToJob(DataSet iDataSet)
        {
            LoadDataSet(iDataSet);
            LoadOpenJobsToJob();
            return this.dsMiscAction;
        }

        public DataSet GetOpenJobOrders(DataSet iDataSet)
        {
            LoadDataSet(iDataSet);
            LoadOpenJobOrders();
            return this.dsMiscAction;
        }

        public DataSet GetOrderDetail(DataSet iDataSet, int orderNum)
        {
            LoadDataSet(iDataSet);
            LoadOrderDetail(orderNum);
            return this.dsMiscAction;
        }

    }
}

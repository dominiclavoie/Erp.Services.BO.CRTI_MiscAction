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
using Newtonsoft.Json;

namespace Erp.BO.CRTI_MiscAction
{
    class GestionJobUsinage : BaseMiscAction, IMiscAction
    {
        public GestionJobUsinage(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session){}

        private IEnumerable<FSmObject> SelectPartSug(string partClass)
        {
            IEnumerable<FSmObject> jobs = (from pd in this.Db.PartDtl
                                            join pt in this.Db.Part on new { pd.Company, pd.PartNum, ClassID = partClass }
                                                                equals new { pt.Company, pt.PartNum, pt.ClassID }
                                            join ps in this.Db.PartSug on new { pd.Company, pd.PartNum, pd.Plant, pd.OrderNum, pd.OrderLine, pd.OrderRelNum, Source = "Order", Type = "New", Classifier = "Mfg" }
                                                                   equals new { ps.Company, ps.PartNum, ps.Plant, ps.OrderNum, ps.OrderLine, ps.OrderRelNum, ps.Source, ps.Type, ps.Classifier }
                                            join pp in this.Db.PartPlant on new { ps.Company, ps.PartNum, ps.Plant }
                                                                     equals new { pp.Company, pp.PartNum, pp.Plant }
                                            join jp in this.Db.JobProd on new { ps.Company, ps.OrderNum, ps.OrderLine, ps.OrderRelNum }
                                                                   equals new {  jp.Company, jp.OrderNum, jp.OrderLine, jp.OrderRelNum } into jps
                                            from jp in jps.DefaultIfEmpty()
                                            where pd.Company == "01" &&
                                                  pd.Type == "Mtl" &&
                                                  pd.SourceFile == "OR" &&
                                                  jp.Company == null
                                            select new FSmObject
                                            {
                                                PartNum = ps.PartNum,
                                                RevisionNum = ps.SugRevisionNum,
                                                OrderNum = ps.OrderNum,
                                                OrderLine = ps.OrderLine,
                                                OrderRelNum = ps.OrderRelNum,
                                                ReqDueDate = ps.DueDate ?? DateTime.Now,
                                                DueDateIsNull = ps.DueDate == null,
                                                DaysOfSupply = pp.DaysOfSupply,
                                                SugQty = ps.SugQty,
                                                SugUOM = ps.SugQtyUOM,
                                                RecKey = pd.SysRowID
                                            });
            return jobs;
        }
        private List<FSmObject> SelectFSmObjects()
        {
            return SelectPartSug("FSm").Where(tt => !tt.DueDateIsNull).OrderBy(tt => tt.ReqDueDate).ThenBy(x => x.OrderNum).ThenBy(y => y.OrderLine).ThenBy(z => z.OrderRelNum).ToList();
        }
        private void CreateNewJobs()
        {
            FSmManager FSmManager = new FSmManager();
            FSmManager.AddRange(SelectFSmObjects());
            if (!FSmManager.Any())
            {
                return;
            }
            JobManagerController jmc = new JobManagerController(this.Db, this.Session);
            List<string> jobNums;
            string message;
            if (!jmc.CreateJobs(FSmManager.GetItems(), out jobNums, out message, false))
            {
                throw new BLException(message);
            }
            if (!jobNums.Any())
            {
                return;
            }
            JobStatusController jsc = new JobStatusController(this.Db, this.Session);
            if (!jsc.ReleaseJobs(jobNums, out message))
            {
                throw new BLException(message);
            }
            ScheduleEngineController sec = new ScheduleEngineController(this.Db, this.Session);
            if (!sec.ScheduleJobs(jobNums, out message))
            {
                throw new BLException(message);
            }
        }
        private IEnumerable<UpdateQtyObject> SelectJobBatchOrderQtyChange(string partClass)
        {
            IEnumerable<UpdateQtyObject> jbatchs = (from pd in this.Db.PartDtl.AsEnumerable()
                                           join pt in this.Db.Part.AsEnumerable() on new { pd.Company, pd.PartNum, ClassID = partClass }
                                                                              equals new { pt.Company, pt.PartNum, pt.ClassID }
                                           join ps in this.Db.PartSug.AsEnumerable() on new { pd.Company, pd.PartNum, pd.Plant, pd.OrderNum, pd.OrderLine, pd.OrderRelNum, Source = "Order", Type = "QTY", Classifier = "Mfg" }
                                                                                 equals new { ps.Company, ps.PartNum, ps.Plant, ps.OrderNum, ps.OrderLine, ps.OrderRelNum, ps.Source, ps.Type, ps.Classifier }
                                           join jp in this.Db.JobProd.AsEnumerable() on new { ps.Company, ps.OrderNum, ps.OrderLine, ps.OrderRelNum }
                                                                                 equals new { jp.Company, jp.OrderNum, jp.OrderLine, jp.OrderRelNum }
                                           join bl in this.Db.UD104.AsEnumerable() on new { jp.Company, IDRow = jp.SysRowID.ToString(), Type = "JobOper" }
                                                                               equals new { bl.Company, IDRow = bl.Character10, Type = bl.Key2 }
                                           join jb in this.Db.UD105A.AsEnumerable() on new { bl.Company, IDLigne = bl.ShortChar01, Type = "Production" }
                                                                                equals new { jb.Company, IDLigne = jb.ChildKey3, Type = jb.ChildKey1 }
                                           where pd.Company == "01" &&
                                                 pd.Type == "Mtl" &&
                                                 pd.SourceFile == "OR" &&
                                                 ps.SugQty != jb.Number02
                                           select new UpdateQtyObject
                                           {
                                               NewQty = ps.SugQty,
                                               JbBatch = jb
                                           });
            return jbatchs;
        }
        private void UpdateOrderQty()
        {
            IEnumerable<UpdateQtyObject> jbBatchs = SelectJobBatchOrderQtyChange("FSm");
            if(!jbBatchs.Any())
            {
                return;
            }
            Erp.Internal.Lib.ValidatingTransactionScope txScope = new Erp.Internal.Lib.ValidatingTransactionScope(this.Db);
            try
            {
                foreach(UpdateQtyObject jbBatch in jbBatchs)
                {
                    jbBatch.JbBatch.Number02 = jbBatch.NewQty;
                    ((IceDataContext)(this.Db)).Validate<UD105A>(jbBatch.JbBatch);
                }
                txScope.Complete();
            }
            finally
            {
                ((IDisposable)(object)txScope)?.Dispose();
            }
        }
        private IEnumerable<UpdateDateObject> SelectJobOrderDateChange(string partClass)
        {
            IEnumerable<UpdateDateObject> jProds = (from pd in this.Db.PartDtl.AsEnumerable()
                                                    join pt in this.Db.Part.AsEnumerable() on new { pd.Company, pd.PartNum, ClassID = partClass }
                                                                                       equals new { pt.Company, pt.PartNum, pt.ClassID }
                                                    join ps in this.Db.PartSug.AsEnumerable() on new { pd.Company, pd.PartNum, pd.Plant, pd.OrderNum, pd.OrderLine, pd.OrderRelNum, Source = "Order", Type = "Dat", Classifier = "Mfg" }
                                                                                          equals new { ps.Company, ps.PartNum, ps.Plant, ps.OrderNum, ps.OrderLine, ps.OrderRelNum, ps.Source, ps.Type, ps.Classifier }
                                                    join jp in this.Db.JobProd.AsEnumerable() on new { ps.Company, ps.OrderNum, ps.OrderLine, ps.OrderRelNum }
                                                                                          equals new { jp.Company, jp.OrderNum, jp.OrderLine, jp.OrderRelNum }
                                                    join jh in this.Db.JobHead.AsEnumerable() on new { jp.Company, jp.JobNum }
                                                                                          equals new { jh.Company, jh.JobNum }
                                                    where pd.Company == "01" &&
                                                          pd.Type == "Mtl" &&
                                                          pd.SourceFile == "OR" &&
                                                          ps.SugDate != null &&
                                                          jh.JobClosed == false &&
                                                          jh.JobEngineered == true &&
                                                          jh.JobReleased == true &&
                                                          jh.JobFirm == true
                                                    select new UpdateDateObject
                                                    {
                                                        NewDate = ps.SugDate,
                                                        JobNum = jp.JobNum,
                                                        JobHead = jh
                                                    });
            return jProds;
        }
        private void UpdateOrderDate()
        {
            IEnumerable<UpdateDateObject> jProds = SelectJobOrderDateChange("FSm");
            if (!jProds.Any())
            {
                return;
            }
            JobEntryController jec = new JobEntryController(this.Db, this.Session);
            string message;
            if (!jec.UpdateJobDates(jProds.ToList(), out message))
            {
                throw new BLException(message);
            }
        }
        public void Process()
        {
            CreateNewJobs();
            UpdateOrderQty();
            UpdateOrderDate();
        }
        public class UpdateQtyObject
        {
            #region Properties
            private decimal newQty;
            private UD105A jbBatch;
            public decimal NewQty { get => newQty; set => newQty = value; }
            public UD105A JbBatch { get => jbBatch; set => jbBatch = value; }
            #endregion
            public UpdateQtyObject() { }
        }
        public class UpdateDateObject
        {
            #region Properties
            private DateTime? newDate;
            private string jobNum;
            private JobHead jobHead;
            public DateTime? NewDate { get => newDate; set => newDate = value; }
            public string JobNum { get => jobNum; set => jobNum = value; }
            public JobHead JobHead { get => jobHead; set => jobHead = value; }
            #endregion
            public UpdateDateObject() { }
        }
        public class FSmManager
        {
            private List<FSmPart> items;

            public FSmManager()
            {
                items = new List<FSmPart>();
            }
            
            public void AddRange(List<FSmObject> collection)
            {
                if(!collection.Any())
                {
                    return;
                }
                foreach(FSmObject FSmObject in collection)
                {
                    FSmPart FSmPart = items.Where(i => i.PartNum == FSmObject.PartNum && i.RevisionNum == FSmObject.RevisionNum).FirstOrDefault();
                    if (FSmPart == null)
                    {
                        items.Add(new FSmPart(FSmObject));
                    }
                    else
                    {
                        FSmPart.Add(FSmObject);
                    }
                }
            }
            public bool Any()
            {
                return this.items.Any();
            }
            public List<FSmList> GetItems()
            {
                List<FSmList> objects = new List<FSmList>();
                foreach(FSmPart FSmPart in this.items)
                {
                    objects.AddRange(FSmPart.Items);
                }
                
                return objects;
            }
        }
        public class FSmPart
        {
            #region Properties
            private string partNum;
            private string revisionNum;
            private List<FSmList> items;
            public string PartNum { get => partNum; }
            public string RevisionNum { get => revisionNum; }
            public List<FSmList> Items { get => items; }
            #endregion

            public FSmPart(FSmObject FSmObject)
            {
                this.partNum = FSmObject.PartNum;
                this.revisionNum = FSmObject.RevisionNum;
                this.items = new List<FSmList>();
                Add(FSmObject);
            }
            public void Add(FSmObject FSmObject)
            {
                if (FSmObject.DaysOfSupply == 0)
                {
                    items.Add(new FSmList(FSmObject, this));
                }
                else
                {
                    FSmList FSmList = items.Where(i => i.DateMin <= FSmObject.ReqDueDate && i.DateMax >= FSmObject.ReqDueDate).FirstOrDefault();
                    if (FSmList == null)
                    {
                        items.Add(new FSmList(FSmObject, this));
                    }
                    else
                    {
                        FSmList.Add(FSmObject);
                    }
                }
            }

        }
        public class FSmList : IJobManagerList
        {
            #region Properties
            private DateTime dateMin;
            private DateTime dateMax;
            private DateTime reqDueDateMax;
            private int daysOfSupply;
            private List<FSmObject> items;
            private FSmPart parent;
            public DateTime DateMin { get => dateMin; }
            public DateTime DateMax { get => dateMax; }
            public string PartNum { get => parent.PartNum; }
            public string RevisionNum { get => parent.RevisionNum; }
            public IEnumerable<IJobManagerObject> Items { get => items; }
            #endregion
            private void SetMinMax()
            {
                this.dateMin = this.reqDueDateMax.AddDays(0);
                this.dateMax = this.reqDueDateMax.AddDays(this.daysOfSupply);
            }
            public FSmList(FSmObject FSmObject, FSmPart _parent)
            {
                this.parent = _parent;
                this.items = new List<FSmObject>();
                this.daysOfSupply = FSmObject.DaysOfSupply;
                this.reqDueDateMax = FSmObject.ReqDueDate;
                SetMinMax();
                Add(FSmObject);
            }
            public void Add(FSmObject FSmObject)
            {
                this.items.Add(FSmObject);
            }
            
        }

        public class FSmObject : IJobManagerObject
        {
            #region Properties
            private string partNum;
            private string revisionNum;
            private int orderNum;
            private int orderLine;
            private int orderRelNum;
            private DateTime reqDueDate;
            private bool dueDateIsNull;
            private int daysOfSupply;
            private decimal sugQty;
            private string sugUOM;
            private Guid recKey;
            
            public string PartNum { get => partNum; set => partNum = value; }
            public string RevisionNum { get => revisionNum; set => revisionNum = value; }
            public int OrderNum { get => orderNum; set => orderNum = value; }
            public int OrderLine { get => orderLine; set => orderLine = value; }
            public int OrderRelNum { get => orderRelNum; set => orderRelNum = value; }
            public DateTime ReqDueDate { get => reqDueDate; set => reqDueDate = value; }
            public bool DueDateIsNull { get => dueDateIsNull; set => dueDateIsNull = value; }
            public int DaysOfSupply { get => daysOfSupply; set => daysOfSupply = value; }
            public decimal SugQty { get => sugQty; set => sugQty = value; }
            public string SugUOM { get => sugUOM; set => sugUOM = value; }
            public Guid RecKey { get => recKey; set => recKey = value; }
            #endregion
            public FSmObject() { }
            public FSmObject(string _partNum, string _revisionNum, int _orderNum, int _orderLine, int _orderRelNum, DateTime? _reqDueDate, int _daysOfSupply, decimal _sugQty, string _sugUOM, decimal _qtyPer, Guid _recKey)
            {
                this.partNum = _partNum;
                this.revisionNum = _revisionNum;
                this.orderNum = _orderNum;
                this.orderLine = _orderLine;
                this.orderRelNum = _orderRelNum;
                this.reqDueDate = _reqDueDate ?? DateTime.Now;
                this.dueDateIsNull = _reqDueDate == null;
                this.daysOfSupply = _daysOfSupply;
                this.sugQty = _sugQty;
                this.sugUOM = _sugUOM;
                this.recKey = _recKey;
            }
        }

    }
}

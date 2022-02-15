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
    class PostMRP : BaseMiscAction, IMiscAction
    {
        public PostMRP(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session){}

        private IEnumerable<BPSmObject> SelectPartSug(string partClass)
        {
            IEnumerable<BPSmObject> jobs = (from pd in this.Db.PartDtl
                                            join pt in this.Db.Part on new { pd.Company, pd.PartNum, ClassID = partClass }
                                                                equals new { pt.Company, pt.PartNum, pt.ClassID }
                                            join ps in this.Db.PartSug on new { pd.Company, pd.PartNum, pd.Plant, pd.JobNum, pd.AssemblySeq, pd.JobSeq, Source = "Job", Type = "New", Classifier = "Mfg" }
                                                                   equals new { ps.Company, ps.PartNum, ps.Plant, ps.JobNum, ps.AssemblySeq, ps.JobSeq, ps.Source, ps.Type, ps.Classifier }
                                            join pp in this.Db.PartPlant on new { ps.Company, ps.PartNum, ps.Plant }
                                                                     equals new { pp.Company, pp.PartNum, pp.Plant }
                                            join jm in this.Db.JobMtl on new { pd.Company, pd.JobNum, pd.AssemblySeq, pd.JobSeq }
                                                                  equals new { jm.Company, jm.JobNum, jm.AssemblySeq, JobSeq = jm.MtlSeq }
                                            where pd.Company == this.Session.CompanyID &&
                                                  pd.Type == "Mtl" &&
                                                  pd.SourceFile == "JM"
                                            select new BPSmObject
                                            {
                                                TargetJobNum = ps.JobNum,
                                                PartNum = ps.PartNum,
                                                RevisionNum = ps.SugRevisionNum,
                                                ReqDueDate = ps.DueDate ?? DateTime.Now,
                                                DueDateIsNull = ps.DueDate == null,
                                                DaysOfSupply = pp.DaysOfSupply,
                                                SugQty = ps.SugQty,
                                                SugUOM = ps.SugQtyUOM,
                                                QtyPer = jm.QtyPer,
                                                RecKey = pd.SysRowID
                                            });
            return jobs;
        }
        private List<BPSmObject> SelectBPSmObjects()
        {
            return SelectPartSug("BPSm").Where(tt => !tt.DueDateIsNull).OrderBy(tt => tt.ReqDueDate).ThenBy(x => Convert.ToInt32(x.TargetJobNum)).ToList();
        }
        private List<BPSmObject> SelectMelangeObjects(List<string> jobNums)
        {
            return SelectPartSug("FMel").Where(tt => jobNums.Contains(tt.TargetJobNum)).ToList();
        }
        public void Process()
        {
            BPSmManager bPSmManager = new BPSmManager();
            bPSmManager.AddRange(SelectBPSmObjects());
            if (!bPSmManager.Any())
            {
                return;
            }
            JobManagerController jmc = new JobManagerController(this.Db, this.Session);
            List<string> jobNums;
            string message;
            if(!jmc.CreateJobs(bPSmManager.GetItems(), out jobNums, out message))
            {
                return;
            }
            bPSmManager = new BPSmManager();
            bPSmManager.AddRange(SelectMelangeObjects(jobNums));
            if (!bPSmManager.Any())
            {
                return;
            }
            jmc.CreateJobs(bPSmManager.GetItems(), out jobNums, out message);
        }

        public class BPSmManager
        {
            private List<BPSmPart> items;

            public BPSmManager()
            {
                items = new List<BPSmPart>();
            }
            
            public void AddRange(List<BPSmObject> collection)
            {
                if(!collection.Any())
                {
                    return;
                }
                foreach(BPSmObject bPSmObject in collection)
                {
                    BPSmPart bPSmPart = items.Where(i => i.PartNum == bPSmObject.PartNum && i.RevisionNum == bPSmObject.RevisionNum).FirstOrDefault();
                    if (bPSmPart == null)
                    {
                        items.Add(new BPSmPart(bPSmObject));
                    }
                    else
                    {
                        bPSmPart.Add(bPSmObject);
                    }
                }
            }
            public bool Any()
            {
                return this.items.Any();
            }
            public List<BPSmList> GetItems()
            {
                List<BPSmList> objects = new List<BPSmList>();
                foreach(BPSmPart bPSmPart in this.items)
                {
                    objects.AddRange(bPSmPart.Items);
                }
                
                return objects;
            }
        }
        public class BPSmPart
        {
            #region Properties
            private string partNum;
            private string revisionNum;
            private List<BPSmList> items;
            public string PartNum { get => partNum; }
            public string RevisionNum { get => revisionNum; }
            public List<BPSmList> Items { get => items; }
            #endregion

            public BPSmPart(BPSmObject bPSmObject)
            {
                this.partNum = bPSmObject.PartNum;
                this.revisionNum = bPSmObject.RevisionNum;
                this.items = new List<BPSmList>();
                Add(bPSmObject);
            }
            public void Add(BPSmObject bPSmObject)
            {
                if (bPSmObject.DaysOfSupply == 0)
                {
                    items.Add(new BPSmList(bPSmObject, this));
                }
                else
                {
                    BPSmList bPSmList = items.Where(i => i.DateMin <= bPSmObject.ReqDueDate && i.DateMax >= bPSmObject.ReqDueDate).FirstOrDefault();
                    if (bPSmList == null)
                    {
                        items.Add(new BPSmList(bPSmObject, this));
                    }
                    else
                    {
                        bPSmList.Add(bPSmObject);
                    }
                }
            }

        }
        public class BPSmList : IJobManagerList
        {
            #region Properties
            private DateTime dateMin;
            private DateTime dateMax;
            private DateTime reqDueDateMax;
            private int daysOfSupply;
            private List<BPSmObject> items;
            private BPSmPart parent;
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
            public BPSmList(BPSmObject bPSmObject, BPSmPart _parent)
            {
                this.parent = _parent;
                this.items = new List<BPSmObject>();
                this.daysOfSupply = bPSmObject.DaysOfSupply;
                this.reqDueDateMax = bPSmObject.ReqDueDate;
                SetMinMax();
                Add(bPSmObject);
            }
            public void Add(BPSmObject bPSmObject)
            {
                this.items.Add(bPSmObject);
            }
            
        }

        public class BPSmObject : IJobManagerObject
        {
            #region Properties
            private string partNum;
            private string revisionNum;
            private string targetJobNum;
            private int targetAssemblySeq;
            private int targetMtlSeq;
            private DateTime reqDueDate;
            private bool dueDateIsNull;
            private int daysOfSupply;
            private decimal sugQty;
            private string sugUOM;
            private Guid recKey;
            private decimal qtyPer;
            
            public string PartNum { get => partNum; set => partNum = value; }
            public string RevisionNum { get => revisionNum; set => revisionNum = value; }
            public string TargetJobNum { get => targetJobNum; set => targetJobNum = value; }
            public int TargetAssemblySeq { get => targetAssemblySeq; set => targetAssemblySeq = value; }
            public int TargetMtlSeq { get => targetMtlSeq; set => targetMtlSeq = value; }
            public DateTime ReqDueDate { get => reqDueDate; set => reqDueDate = value; }
            public bool DueDateIsNull { get => dueDateIsNull; set => dueDateIsNull = value; }
            public int DaysOfSupply { get => daysOfSupply; set => daysOfSupply = value; }
            public decimal SugQty { get => sugQty; set => sugQty = value; }
            public string SugUOM { get => sugUOM; set => sugUOM = value; }
            public decimal QtyPer { get => qtyPer; set => qtyPer = value; }
            public Guid RecKey { get => recKey; set => recKey = value; }
            public decimal BarLength { get { return this.qtyPer * 1000m; } }
            public decimal BarQty { get { return this.qtyPer > 0m ? this.sugQty / qtyPer : 0m; } }
            #endregion
            public BPSmObject() { }
            public BPSmObject(string _partNum, string _revisionNum, string _targetJobNum, int _targetAssemblySeq, int _targetMtlSeq, DateTime? _reqDueDate, int _daysOfSupply, decimal _sugQty, string _sugUOM, decimal _qtyPer, Guid _recKey)
            {
                this.partNum = _partNum;
                this.revisionNum = _revisionNum;
                this.targetJobNum = _targetJobNum;
                this.targetAssemblySeq = _targetAssemblySeq;
                this.targetMtlSeq = _targetMtlSeq;
                this.reqDueDate = _reqDueDate ?? DateTime.Now;
                this.dueDateIsNull = _reqDueDate == null;
                this.daysOfSupply = _daysOfSupply;
                this.sugQty = _sugQty;
                this.sugUOM = _sugUOM;
                this.qtyPer = _qtyPer;
                this.recKey = _recKey;
            }
        }

    }
}

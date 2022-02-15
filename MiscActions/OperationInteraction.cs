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
    class OperationInteraction : BaseMiscAction, IMiscAction
    {
        private static Func<ErpContext, string, IEnumerable<UD12>> selectOperationInteractionQuery;
        private static Func<ErpContext, string, string, IEnumerable<string>> selectInteractionsForOpCodeQuery;
        private Dictionary<string, IOperationInteraction> opInteractions;

        public OperationInteraction(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session)
        {
            this.opInteractions = new Dictionary<string, IOperationInteraction>();
        }

        #region Private Members

        private IEnumerable<UD12> SelectOperationInteraction(string company)
        {
            if (selectOperationInteractionQuery == null)
            {
                selectOperationInteractionQuery = DBExpressionCompiler.Compile<ErpContext, string, UD12>((Expression<Func<ErpContext, string, IEnumerable<UD12>>>)((ErpContext context, string company_ex) => context.UD12.Where((UD12 row) => row.Company == company_ex)), (Epicor.Data.Cache)0, true);
            }
            return selectOperationInteractionQuery(Db, company);
        }

        private IEnumerable<string> SelectInteractionsForOpCodeQuery(string company, string opCode)
        {
            if (selectInteractionsForOpCodeQuery == null)
            {
                selectInteractionsForOpCodeQuery = DBExpressionCompiler.Compile<ErpContext, string, string, string>((Expression<Func<ErpContext, string, string, IEnumerable<string>>>)((ErpContext context, string company_ex, string opCode_ex) => context.UD12.Where((UD12 row) => row.Company == company_ex && row.Key1 == opCode_ex).GroupBy((UD12 row) => row.Key2).Select(tt => tt.Key)), (Epicor.Data.Cache)0, true);
            }
            return selectInteractionsForOpCodeQuery(Db, company, opCode);
        }

        private bool InteractionExists(string key)
        {
            if( this.opInteractions.Count == 0 )
            {
                GetOperationInteractions();
            }
            return this.opInteractions.ContainsKey(key);
        }

        private IOperationInteraction GetOperationInteractionType(string type)
        {
            IOperationInteraction oi = null;
            switch (type)
            {
                case "NoFGF":
                    oi = new NoFGFInteraction();
                    break;
                case "IsOperGeneral":
                    oi = new OperGeneralInteraction();
                    break;
                case "NoLot":
                    oi = new NoLotInteraction();
                    break;
                case "IsSetup":
                    oi = new IsSetupInteraction();
                    break;
                case "ApplyOnFirstJob":
                    oi = new ApplyOnFirstJobInteraction();
                    break;
                case "DenyConcurrentClockIn":
                    oi = new DenyConcurrentClockInInteraction();
                    break;
                case "UseCurrentLot":
                    oi = new UseCurrentLotInteraction();
                    break;
                case "UseMtlLot":
                    oi = new UseMtlLotInteraction();
                    break;
                case "UsePrevOpLot":
                    oi = new UsePrevOpLotInteraction();
                    break;
                case "ScrapMtlLot":
                    oi = new ScrapMtlLotInteraction();
                    break;
                default:
                    throw new BLException(string.Format("L'intéraction d'opération {0} n'existe pas", type));
            }
            return oi;
        }

        public string GetInteractionsForOpCode(string opCode)
        {
            return string.Join("~", SelectInteractionsForOpCodeQuery(this.Session.CompanyID, opCode).ToArray());
        }

        public void GetOperationInteractions()
        {
            DataTable dt = GetDataTable("OperationInteractions");
            foreach( UD12 row in SelectOperationInteraction(this.Session.CompanyID) )
            {
                string type = row.Key2;
                if ( !this.opInteractions.ContainsKey(type) )
                {
                    this.opInteractions[type] = GetOperationInteractionType(type);
                }
                this.opInteractions[type].Add(row.Key1, row.Character01);
                dt.Rows.Add(type, row.Key1, row.Character01);
            }
            if (this.dsMiscAction.Tables.Contains("OperationInteractions"))
            {
                DataSet ds = new DataSet("MiscAction");
                ds.Tables.Add(dt);
                ds.AcceptChanges();
                this.dsMiscAction.Merge(ds, true, MissingSchemaAction.Ignore);
            }
            else
            {
                this.dsMiscAction.Tables.Add(dt);
            }
            this.dsMiscAction.AcceptChanges();
        }
        private void LoadFromDataSet()
        {
            if (this.dsMiscAction.Tables.Contains("OperationInteractions"))
            {
                foreach( DataRow row in this.dsMiscAction.Tables["OperationInteractions"].Rows )
                {
                    string type = row["Type"].ToString();
                    if (!this.opInteractions.ContainsKey(type))
                    {
                        this.opInteractions[type] = GetOperationInteractionType(type);
                    }
                    this.opInteractions[type].Add(row["OpCode"].ToString(), row["OpCodeRef"].ToString());
                }
            }
        }
        private interface IOperationInteraction
        {
            void Add(string key1, string key2);
            bool ContainsOperation(string opCode);
        }
        private abstract class BaseInteraction
        {
            protected List<string> opCodes;
            public BaseInteraction()
            {
                opCodes = new List<string>();
            }
            public virtual void Add(string key1, string key2)
            {
                opCodes.Add(key1);
            }
            public bool ContainsOperation(string opCode)
            {
                return opCodes.Contains(opCode);
            }
        }
        private class NoFGFInteraction : BaseInteraction, IOperationInteraction
        {
            public NoFGFInteraction() : base() { }
        }
        private class OperGeneralInteraction : BaseInteraction, IOperationInteraction
        {
            public OperGeneralInteraction() : base() { }
        }
        private class NoLotInteraction : BaseInteraction, IOperationInteraction
        {
            public NoLotInteraction() : base() { }
        }
        private class IsSetupInteraction : BaseInteraction, IOperationInteraction
        {
            private Dictionary<string, string> setupReferences;
            public IsSetupInteraction() : base()
            {
                setupReferences = new Dictionary<string, string>();
            }
            public override void Add(string key1, string key2)
            {
                base.Add(key1, key2);
                setupReferences[key1] = key2;
            }
            public bool GetOperationSetupRef(string opCode, out string opRef)
            {
                opRef = "";
                if (!setupReferences.ContainsKey(opCode))
                {
                    return false;
                }
                opRef = setupReferences[opCode];
                return true;
            }
        }
        private class ApplyOnFirstJobInteraction : BaseInteraction, IOperationInteraction
        {
            public ApplyOnFirstJobInteraction() : base() { }
        }
        private class UseCurrentLotInteraction : BaseInteraction, IOperationInteraction
        {
            public UseCurrentLotInteraction() : base() { }
        }
        private class UseMtlLotInteraction : BaseInteraction, IOperationInteraction
        {
            public UseMtlLotInteraction() : base() { }
        }

        private class UsePrevOpLotInteraction : BaseInteraction, IOperationInteraction
        {
            public UsePrevOpLotInteraction() : base() { }
        }
        private class DenyConcurrentClockInInteraction : BaseInteraction, IOperationInteraction
        {
            private Dictionary<string, List<string>> concurrentRestrictions;
            public DenyConcurrentClockInInteraction() : base()
            {
                concurrentRestrictions = new Dictionary<string, List<string>>();
            }
            public override void Add(string key1, string key2)
            {
                base.Add(key1, key2);
                if (!concurrentRestrictions.ContainsKey(key1))
                {
                    concurrentRestrictions[key1] = new List<string>();
                }
                concurrentRestrictions[key1].Add(key2);
            }
            public List<string> GetPunchOperationConcurrentRestrictions(string opCode)
            {
                List<string> list = new List<string>();
                if (concurrentRestrictions.ContainsKey(opCode))
                {
                    list = concurrentRestrictions[opCode];
                }
                return list;
            }
        }
        private class ScrapMtlLotInteraction : BaseInteraction, IOperationInteraction
        {
            private Dictionary<string, List<string>> scrapPlaceHolder;
            public ScrapMtlLotInteraction() : base()
            {
                scrapPlaceHolder = new Dictionary<string, List<string>>();
            }
            public override void Add(string key1, string key2)
            {
                base.Add(key1, key2);
                if (!scrapPlaceHolder.ContainsKey(key1))
                {
                    scrapPlaceHolder[key1] = new List<string>();
                }
                scrapPlaceHolder[key1].Add(key2);
            }
            public string GetScrapPlaceHolder(string opCode)
            {
                List<string> list = new List<string>();
                if (scrapPlaceHolder.ContainsKey(opCode))
                {
                    list = scrapPlaceHolder[opCode];
                }
                if (list.Any())
                {
                    return list[0];
                }
                return "";
            }
        }

        #endregion

        public override DataTable[] GetDataTable()
        {
            DataTable dt = new DataTable("OperationInteractions");
            dt.Locale = CultureInfo.InvariantCulture;
            dt.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("Type", typeof(string)),
                new DataColumn("OpCode", typeof(string)),
                new DataColumn("OpCodeRef", typeof(string))
            });
            return new DataTable[] { dt };

        }
        public DataSet LoadInternal(DataSet iDataSet)
        {
            LoadDataSet(iDataSet);
            LoadFromDataSet();
            if (this.opInteractions.Count == 0)
            {
                GetOperationInteractions();
            }
            return this.dsMiscAction;
        }
        public DataSet OperationIs(out bool result, string key, string opCode, DataSet iDataSet)
        {
            result = false;
            LoadDataSet(iDataSet);
            LoadFromDataSet();
            if (InteractionExists(key))
            {
                result = this.opInteractions[key].ContainsOperation(opCode);
            }
            return this.dsMiscAction;
        }

        public bool OperationIs(string key, string opCode)
        {
            if (this.opInteractions.ContainsKey(key))
            {
                return this.opInteractions[key].ContainsOperation(opCode);
            }
            return false;
        }

        public bool GetOperationSetupRef(string opCode, out string opRef)
        {
            opRef = "";
            if (!this.opInteractions.ContainsKey("IsSetup"))
            {
                return false;
            }
            bool flag = ((IsSetupInteraction)this.opInteractions["IsSetup"]).GetOperationSetupRef(opCode, out opRef);
            return flag;
        }

    }
}

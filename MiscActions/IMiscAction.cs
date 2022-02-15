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
    interface IMiscAction
    {
        DataTable[] GetDataTable();
    }
    abstract class BaseMiscAction
    {
        protected DataSet dsMiscAction;
        protected Erp.ErpContext Db;
        protected Epicor.Hosting.Session Session;

        public DataSet DsMiscAction { get => dsMiscAction; }

        public BaseMiscAction(Erp.ErpContext db, Epicor.Hosting.Session session)
        {
            this.Db = db;
            this.Session = session;
            this.dsMiscAction = new DataSet("MiscAction");
        }
        public virtual DataTable[] GetDataTable() { return null; }
        protected DataTable GetDataTable(string tableName)
        {
            return GetDataTable().Where(d => d.TableName == tableName).FirstOrDefault();
        }
        protected void MergeDataTable(DataTable dt, bool resetRows)
        {
            if (this.dsMiscAction.Tables.Contains(dt.TableName))
            {
                if (resetRows)
                {
                    this.dsMiscAction.Tables[dt.TableName].Rows.Clear();
                }
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
        protected void ClearRows(string[] except)
        {
            DataTable[] dts = GetDataTable();
            foreach (DataTable dt in dts)
            {
                if (this.dsMiscAction.Tables.Contains(dt.TableName) && !except.Contains(dt.TableName))
                {
                    this.dsMiscAction.Tables[dt.TableName].Rows.Clear();
                }
            }
            this.dsMiscAction.AcceptChanges();
        }
        protected void LoadDataSet(DataSet dataSet)
        {
            this.dsMiscAction = dataSet.Copy();
            DataSetLoaded();
        }
        protected virtual void DataSetLoaded() { }
    }
}

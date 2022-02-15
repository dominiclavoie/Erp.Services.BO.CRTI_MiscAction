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
    class GestionOperation : BaseMiscAction, IMiscAction
    {

        public GestionOperation(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session) {}

        public override DataTable[] GetDataTable()
        {
            DataTable dt = new DataTable("OperationRaisonRejet");
            dt.Locale = CultureInfo.InvariantCulture;
            dt.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("CodeRaison", typeof(string)),
                new DataColumn("Description", typeof(string)),
                new DataColumn("Sélectionner", typeof(bool))
            });
            DataTable dt2 = new DataTable("RebutProductionRaisonRejet");
            dt2.Locale = CultureInfo.InvariantCulture;
            dt2.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("CodeRaison", typeof(string)),
                new DataColumn("Description", typeof(string))
            });
            return new DataTable[] { dt, dt2 };
        }

        private string[] GetSelectedScrapReasonForOperation(string opCode)
        {
            var operation = (from op in this.Db.OpMaster
                             where op.Company == this.Session.CompanyID &&
                                   op.OpCode == opCode
                             select op).FirstOrDefault();
            if(operation == null)
            {
                return new string[] { };
            }
            return operation.UDField<string>("UD_RaisonRejet_c", false).Split('~');
        }

        private void GetAvailableListScrapReasonForOperation(string opCode)
        {
            DataTable dtOperationRaisonRejet = GetDataTable("OperationRaisonRejet");
            var reasons = (from rs in this.Db.Reason.AsEnumerable()
                           where rs.Company == this.Session.CompanyID &&
                                 rs.ReasonType == "S"
                           select new
                           {
                               rs.ReasonCode,
                               rs.Description
                           });
            string[] selectedReasons = GetSelectedScrapReasonForOperation(opCode);
            foreach (var reason in reasons)
            {
                dtOperationRaisonRejet.Rows.Add(reason.ReasonCode, reason.Description, selectedReasons.Contains(reason.ReasonCode));
            }
            MergeDataTable(dtOperationRaisonRejet, true);
        }

        private void GetListScrapReasonForOperation(string opCode)
        {
            DataTable dtRebutProductionRaisonRejet = GetDataTable("RebutProductionRaisonRejet");
            string[] selectedReasons = GetSelectedScrapReasonForOperation(opCode);
            var reasons = (from rs in this.Db.Reason.AsEnumerable()
                           where rs.Company == this.Session.CompanyID &&
                                 rs.ReasonType == "S" &&
                                 selectedReasons.Contains(rs.ReasonCode)
                           select new
                           {
                               rs.ReasonCode,
                               rs.Description
                           });
            foreach (var reason in reasons)
            {
                dtRebutProductionRaisonRejet.Rows.Add(reason.ReasonCode, reason.Description);
            }
            MergeDataTable(dtRebutProductionRaisonRejet, true);
        }

        public DataSet GetAvailableListScrapReasonForOperation(string opCode, DataSet iDataSet)
        {
            LoadDataSet(iDataSet);
            GetAvailableListScrapReasonForOperation(opCode);
            return this.dsMiscAction;
        }

        public DataSet GetListScrapReasonForOperation(string opCode, DataSet iDataSet)
        {
            LoadDataSet(iDataSet);
            GetListScrapReasonForOperation(opCode);
            return this.dsMiscAction;
        }

    }
}
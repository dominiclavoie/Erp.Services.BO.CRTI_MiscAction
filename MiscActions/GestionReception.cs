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
    class GestionReception : BaseMiscAction, IMiscAction
    {
        public GestionReception(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session) {}

        public void GetPartsReceivedForTag(int vendorNum, string purPoint, string packSlip, out string partNums)
        {
            partNums = string.Empty;
            List<string> parts = (from rh in this.Db.RcvHead
                       join rd in this.Db.RcvDtl on new { rh.Company, rh.VendorNum, rh.PurPoint, rh.PackSlip }
                                             equals new { rd.Company, rd.VendorNum, rd.PurPoint, rd.PackSlip }

                       where rh.VendorNum == vendorNum &&
                             rh.PurPoint == purPoint &&
                             rh.PackSlip == packSlip &&
                             rh.Received == true &&
                             rd.VendorQty > 0m
                       group rd by rd.PartNum into p
                       select p.Key).ToList();
            if (parts.Any())
            {
                partNums = string.Format("'{0}'", string.Join("','", parts));
            }
        }

    }
}

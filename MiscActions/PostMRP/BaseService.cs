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
    abstract class BaseService
    {
        protected Erp.ErpContext Db;
        protected Epicor.Hosting.Session Session;
        public BaseService(Erp.ErpContext db, Epicor.Hosting.Session session)
        {
            this.Db = db;
            this.Session = session;
        }
    }
}

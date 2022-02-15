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
    class JobManager : BaseService
    {
        private Erp.Tablesets.JobClosingTableset ds;
        private Erp.Contracts.JobClosingSvcContract svcJobClosing;
        public JobManager(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session) { }
        public void CloseJobs(List<string> jobNums)
        {
            this.svcJobClosing = Ice.Assemblies.ServiceRenderer.GetService<Erp.Contracts.JobClosingSvcContract>(Db);
            try
            {
                foreach(string jobNum in jobNums)
                {
                    this.ds = new JobClosingTableset();
                    this.svcJobClosing.GetNewJobClosing(ref this.ds);
                    string pcMessage;
                    this.svcJobClosing.OnChangeJobNum(jobNum, ref this.ds, out pcMessage);
                    this.svcJobClosing.OnChangeJobClosed(ref this.ds);
                    bool requiresUserInput;
                    this.svcJobClosing.PreCloseJob(ref this.ds, out requiresUserInput);
                    this.svcJobClosing.CloseJob(ref this.ds, out pcMessage);
                }
            }
            catch (Exception) { }
            finally
            {
                this.svcJobClosing.Dispose();
                this.svcJobClosing = null;
                this.ds = null;
            }
        }
    }
}

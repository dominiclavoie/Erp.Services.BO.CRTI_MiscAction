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
    class JobStatusController : BaseService
    {
        private Erp.Tablesets.JobStatusTableset ds;
        private Erp.Contracts.JobStatusSvcContract svc;
        public JobStatusController(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session) { }
        private void SetAllUnReleased()
        {
            foreach (var jh in this.ds.JobHead.Select(tt => tt))
            {
                if (!jh.JobFirm)
                {
                    continue;
                }
                jh.RowMod = "U";
                jh.JobReleased = false;
                jh.ExtUpdated = true;
                this.svc.ChangeJobHeadJobReleased(false, jh.JobNum, ref this.ds);
            }
        }
        private void SetAllUnEngineered()
        {
            foreach (var jh in this.ds.JobHead.Select(tt => tt))
            {
                if (!jh.JobFirm)
                {
                    continue;
                }
                jh.RowMod = "U";
                jh.JobEngineered = false;
                jh.HeaderSensitive = true;
                jh.ExtUpdated = true;
                this.svc.ChangeJobHeadJobEngineered(false, jh.JobNum, ref this.ds);
            }
        }
        private void SetAllReleased()
        {
            foreach(var jh in this.ds.JobHead.Select(tt => tt))
            {
                if (!jh.JobFirm)
                {
                    continue;
                }
                jh.RowMod = "U";
                jh.JobReleased = true;
                jh.ExtUpdated = true;
                this.svc.ChangeJobHeadJobReleased(true, jh.JobNum, ref this.ds);
            }
        }
        private void SetAllEngineered()
        {
            foreach (var jh in this.ds.JobHead.Select(tt => tt))
            {
                if (!jh.JobFirm)
                {
                    continue;
                }
                jh.RowMod = "U";
                jh.JobEngineered = true;
                jh.HeaderSensitive = false;
                jh.ExtUpdated = true;
                this.svc.ChangeJobHeadJobEngineered(true, jh.JobNum, ref this.ds);
            }
        }
        public bool ReleaseJobs(List<string> jobNums, out string message)
        {
            message = string.Empty;
            if(!jobNums.Any())
            {
                return false;
            }
            this.svc = Ice.Assemblies.ServiceRenderer.GetService<Erp.Contracts.JobStatusSvcContract>(Db);
            try
            {
                string whereClause = string.Format("JobNum in ('{0}')", string.Join("','",jobNums.ToArray()));
                bool morePages;
                this.ds = this.svc.GetRows(whereClause, string.Empty, 0, 0, out morePages);
                if (!this.ds.JobHead.Any())
                {
                    return false;
                }
                SetAllReleased();
                this.svc.MassUpdate(ref this.ds);
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
            finally
            {
                this.svc.Dispose();
                this.svc = null;
                this.ds = null;
            }
        }
        public bool SetUnReadyJobs(List<string> jobNums)
        {
            if (!jobNums.Any())
            {
                return false;
            }
            this.svc = Ice.Assemblies.ServiceRenderer.GetService<Erp.Contracts.JobStatusSvcContract>(Db);
            try
            {
                string whereClause = string.Format("JobNum in ('{0}')", string.Join("','", jobNums.ToArray()));
                bool morePages;
                this.ds = this.svc.GetRows(whereClause, string.Empty, 0, 0, out morePages);
                if (!this.ds.JobHead.Any())
                {
                    return false;
                }
                SetAllUnReleased();
                SetAllUnEngineered();
                this.svc.MassUpdate(ref this.ds);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                this.svc.Dispose();
                this.svc = null;
                this.ds = null;
            }
        }
        public bool SetReadyJobs(List<string> jobNums)
        {
            if (!jobNums.Any())
            {
                return false;
            }
            this.svc = Ice.Assemblies.ServiceRenderer.GetService<Erp.Contracts.JobStatusSvcContract>(Db);
            try
            {
                string whereClause = string.Format("JobNum in ('{0}')", string.Join("','", jobNums.ToArray()));
                bool morePages;
                this.ds = this.svc.GetRows(whereClause, string.Empty, 0, 0, out morePages);
                if (!this.ds.JobHead.Any())
                {
                    return false;
                }
                SetAllReleased();
                SetAllEngineered();
                this.svc.MassUpdate(ref this.ds);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                this.svc.Dispose();
                this.svc = null;
                this.ds = null;
            }
        }
    }
}

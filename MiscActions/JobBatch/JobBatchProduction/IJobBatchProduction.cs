using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Erp.Tables;
using Erp.Tablesets;
using Erp.Services.BO;
using Epicor.Data;
using Ice;
using Ice.Tables;
using Ice.Tablesets;

namespace Erp.BO.CRTI_MiscAction
{
    interface IJobBatchProduction
    {
        void Distribute(DataSet dsJobBatch, ref DataSet dsMiscAction);
        void SubmitLabor(DataSet dsJobBatch, DataSet dsMiscAction, Erp.BO.CRTI_MiscAction.OperationInteraction operationInteraction);
    }
    abstract class BaseJobBatchProduction
    {
        protected Erp.ErpContext Db;
        protected Epicor.Hosting.Session Session;
        protected IEnumerable<JobOper> jobOperDetails;
        protected IEnumerable<JobOper> lastJobOper;
        private static Func<ErpContext, string, IEnumerable<JobOper>> selectJobOperDetailsQuery;
        private static Func<ErpContext, string, string, string, string, string, string, IEnumerable<UD105>> selectHeaderQuery;
        private static Func<ErpContext, string, string, string, string, string, string, IEnumerable<UD105A>> selectProductionQuery;
        private static Func<ErpContext, string, string, string, string, string, string, IEnumerable<UD105A>> selectRebutProductionQuery;
        public BaseJobBatchProduction(Erp.ErpContext db, Epicor.Hosting.Session session)
        {
            this.Db = db;
            this.Session = session;
        }
        protected static void Copy<T>(DataRow row, T obj, params string[] except) where T : class
        {
            Type type = obj.GetType();
            PropertyInfo[] properties = type.GetProperties();
            foreach (PropertyInfo property in properties)
            {
                if (!except.Contains(property.Name) && row.Table.Columns.Contains(property.Name) && property.GetValue(obj) != null)
                {
                    row[property.Name] = property.GetValue(obj);
                }
            }
        }
        protected IEnumerable<UD105> SelectHeader(string company, string key1, string key2, string key3, string key4, string key5)
        {
            if (selectHeaderQuery == null)
            {
                selectHeaderQuery = DBExpressionCompiler.Compile<ErpContext, string, string, string, string, string, string, UD105>((Expression<Func<ErpContext, string, string, string, string, string, string, IEnumerable<UD105>>>)((ErpContext context, string company_ex, string key1_ex, string key2_ex, string key3_ex, string key4_ex, string key5_ex) => context.UD105.Where((UD105 row) => row.Company == company_ex && row.Key1 == key1_ex && row.Key2 == key2_ex && row.Key3 == key3_ex && row.Key4 == key4_ex && row.Key5 == key5_ex)), (Epicor.Data.Cache)0, true);
            }
            return selectHeaderQuery(Db, company, key1, key2, key3, key4, key5);
        }
        protected IEnumerable<UD105A> SelectProduction(string company, string key1, string key2, string key3, string key4, string key5)
        {
            if (selectProductionQuery == null)
            {
                selectProductionQuery = DBExpressionCompiler.Compile<ErpContext, string, string, string, string, string, string, UD105A>((Expression<Func<ErpContext, string, string, string, string, string, string, IEnumerable<UD105A>>>)((ErpContext context, string company_ex, string key1_ex, string key2_ex, string key3_ex, string key4_ex, string key5_ex) => context.UD105A.Where((UD105A row) => row.Company == company_ex && row.Key1 == key1_ex && row.Key2 == key2_ex && row.Key3 == key3_ex && row.Key4 == key4_ex && row.Key5 == key5_ex && row.ChildKey1 == "Production")), (Epicor.Data.Cache)0, true);
            }
            return selectProductionQuery(Db, company, key1, key2, key3, key4, key5);
        }
        protected IEnumerable<UD105A> SelectRebutProduction(string company, string key1, string key2, string key3, string key4, string key5)
        {
            if (selectRebutProductionQuery == null)
            {
                selectRebutProductionQuery = DBExpressionCompiler.Compile<ErpContext, string, string, string, string, string, string, UD105A>((Expression<Func<ErpContext, string, string, string, string, string, string, IEnumerable<UD105A>>>)((ErpContext context, string company_ex, string key1_ex, string key2_ex, string key3_ex, string key4_ex, string key5_ex) => context.UD105A.Where((UD105A row) => row.Company == company_ex && row.Key1 == key1_ex && row.Key2 == key2_ex && row.Key3 == key3_ex && row.Key4 == key4_ex && row.Key5 == key5_ex && row.ChildKey1 == "RebutProduction")), (Epicor.Data.Cache)0, true);
            }
            return selectRebutProductionQuery(Db, company, key1, key2, key3, key4, key5);
        }
        protected bool SelectTargetJobInfos(string barlistID, out string targetJobNum, out int targetAssemblySeq, out int targetMtlSeq)
        {
            targetJobNum = string.Empty;
            targetAssemblySeq = -1;
            targetMtlSeq = -1;
            UD104 barlistJobOper = (from bl in Db.UD104
                                    where bl.Company == this.Session.CompanyID &&
                                          bl.Key2 == "JobOper" &&
                                          bl.ShortChar01 == barlistID
                                    select bl).FirstOrDefault();
            if(barlistJobOper == null)
            {
                return false;
            }
            UD104 barlistJob = (from bl in Db.UD104
                                where bl.Company == this.Session.CompanyID &&
                                      bl.Key2 == "Job"
                                select bl).Where(tt => tt.SysRowID.ToString().Equals(barlistJobOper.Character08)).FirstOrDefault();
            if(barlistJob == null)
            {
                return false;
            }
            string[] jobInfos = barlistJob.Key4.Split('*');
            if(jobInfos.Length != 4)
            {
                return false;
            }
            targetJobNum = jobInfos[1];
            targetAssemblySeq = int.Parse(jobInfos[2]);
            targetMtlSeq = int.Parse(jobInfos[3]);
            return true;
        }
        private IEnumerable<JobOper> SelectJobOperDetails(string company, List<string> jobNums)
        {
            if (selectJobOperDetailsQuery == null)
            {
                selectJobOperDetailsQuery = DBExpressionCompiler.Compile<ErpContext, string, JobOper>(
                    (Expression<Func<ErpContext, string, IEnumerable<JobOper>>>)(
                        (ErpContext context, string company_ex) => (
                            from jh in context.JobHead
                            join jo in context.JobOper on new { jh.Company, jh.JobNum }
                                                   equals new { jo.Company, jo.JobNum }
                            where jh.Company == company_ex &&
                                  jh.JobClosed == false &&
                                  jh.JobComplete == false &&
                                  jh.JobEngineered == true &&
                                  jh.JobReleased == true &&
                                  jo.AssemblySeq == 0
                            select jo
                        )
                    ), (Epicor.Data.Cache)0, true);
            }
            return selectJobOperDetailsQuery(Db, company).Where(tt => jobNums.Contains(tt.JobNum));
        }
        private IEnumerable<JobOper> SelectLastJobOper(List<string> jobNums)
        {
            IEnumerable<JobOper> jobOpDetails = SelectJobOperDetails(this.Session.CompanyID, jobNums);
            var jobOpers = from jo in jobOpDetails
                           group jo by new
                           {
                               jo.JobNum
                           } into grp
                           select new
                           {
                               grp.Key.JobNum,
                               OpSeq = grp.Max(r => r.OprSeq)
                           };
            if (!jobOpers.Any())
            {
                return null;
            }
            return (from jo in jobOpDetails
                    join jp in jobOpers on new { jo.JobNum, jo.OprSeq }
                                    equals new { jp.JobNum, OprSeq = jp.OpSeq }
                    select jo);
        }
        protected void GetJobOperDetails(List<string> jobNums)
        {
            jobOperDetails = SelectJobOperDetails(this.Session.CompanyID, jobNums);
        }
        protected void GetLastJobOper(List<string> jobNums)
        {
            lastJobOper = SelectLastJobOper(jobNums);
        }
        protected bool IsLastJobOper(string jobNum, string opCode)
        {
            if(lastJobOper == null || !lastJobOper.Any())
            {
                return false;
            }
            JobOper jobOp = (from jo in lastJobOper
                             where jo.JobNum == jobNum &&
                                   jo.OpCode == opCode
                             select jo).FirstOrDefault();
            if( jobOp == null)
            {
                return false;
            }
            return true;
        }
        protected bool GetJobOperSeq(string jobNum, string opCode, out int oprSeq)
        {
            oprSeq = -1;
            if (jobOperDetails == null || !jobOperDetails.Any())
            {
                return false;
            }
            JobOper jobOp = (from jo in jobOperDetails
                             where jo.JobNum == jobNum &&
                                   jo.OpCode == opCode
                             select jo).FirstOrDefault();
            if (jobOp == null)
            {
                return false;
            }
            oprSeq = jobOp.OprSeq;
            return true;
        }
        protected void ClearLists()
        {
            jobOperDetails = null;
        }
        protected virtual List<string> GetJobNums(DataSet dsJobBatch)
        {
            List<string> jobNums = new List<string>();
            if (!dsJobBatch.Tables.Contains("ValidationJobBatch"))
            {
                return jobNums;
            }
            foreach(DataRow row in dsJobBatch.Tables["ValidationJobBatch"].Rows)
            {
                string jobNum = row["JobNum"].ToString();
                if(!string.IsNullOrEmpty(jobNum) && !jobNums.Contains(jobNum))
                {
                    jobNums.Add(jobNum);
                }
            }
            return jobNums;
        }
    }
}

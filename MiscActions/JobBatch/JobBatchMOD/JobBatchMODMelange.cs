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
    class JobBatchMODMelange : BaseJobBatchMOD, IJobBatchMOD
    {
        public JobBatchMODMelange(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session) { }
        public DataTable[] GetDataTable()
        {
            DataTable dt = new DataTable("ValidationJobBatch");
            dt.Locale = CultureInfo.InvariantCulture;
            dt.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("EmployeeNum", typeof(string)),
                new DataColumn("ResourceID", typeof(string)),
                new DataColumn("OpCode", typeof(string)),
                new DataColumn("ClockDate", typeof(string)),
                new DataColumn("ClockIn", typeof(int)),
                new DataColumn("ClockOut", typeof(int)),
                new DataColumn("Minutes", typeof(decimal[])),
                new DataColumn("MOD", typeof(decimal)),
                new DataColumn("FGF", typeof(decimal))
            });
            dt.PrimaryKey = new DataColumn[]
            {
                dt.Columns["EmployeeNum"],
                dt.Columns["ResourceID"],
                dt.Columns["OpCode"],
                dt.Columns["ClockDate"],
                dt.Columns["ClockIn"],
                dt.Columns["ClockOut"]
            };

            DataTable dt2 = new DataTable("TempsRepartir");
            dt2.Locale = CultureInfo.InvariantCulture;
            dt2.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("ResourceID", typeof(string)),
                new DataColumn("OpCode", typeof(string)),
                new DataColumn("MOD", typeof(decimal)),
                new DataColumn("FGF", typeof(decimal))
            });
            dt2.PrimaryKey = new DataColumn[]
            {
                dt2.Columns["ResourceID"],
                dt2.Columns["OpCode"]
            };
            return new DataTable[] { dt, dt2 };
        }
        public void ProcessLaborDtl(DataSet dsJobBatch, Erp.BO.CRTI_MiscAction.OperationInteraction operationInteraction, ref DataSet dsMiscAction)
        {
            NumberStyles style = NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands;
            CultureInfo provider = new CultureInfo("fr-CA");
            if (!dsMiscAction.Tables.Contains("ValidationJobBatch"))
            {
                throw new BLException("La table ValidationJobBatch est manquante.");
            }
            DataTable dt = dsMiscAction.Tables["ValidationJobBatch"].Clone();
            TimeCumulator modTimes = new TimeCumulator();
            TimeCumulator fgfTimes = new TimeCumulator();
            foreach (DataRow row in dsJobBatch.Tables["LaborDtl"].Rows)
            {
                string employeeNum = row["EmployeeNum"].ToString();
                string resID = row["ResourceID"].ToString();
                string opCode = row["OpCode"].ToString();
                bool isNoFGF = operationInteraction.OperationIs("NoFGF", opCode);
                decimal clockinTime = decimal.Parse(row["ClockinTime"].ToString(), style, provider);
                int clockIn = Convert.ToInt32(Math.Round((clockinTime * 60m), MidpointRounding.ToEven));
                //int clockIn = Convert.ToInt32(Math.Round((decimal.Parse(row["ClockinTime"].ToString(), style, System.Globalization.CultureInfo.InvariantCulture) * 60m), MidpointRounding.ToEven));
                int startMin = int.Parse(row["ClockInMInute"].ToString());
                int endMin = int.Parse(row["ClockOutMinute"].ToString());
                int clockOut = clockIn + endMin - startMin;
                int nextDay = 0;
                if (clockOut > 1440)
                {
                    nextDay = clockOut - 1440;
                    clockOut = 1440;
                }
                string subDate = row["ClockInDate"].ToString().Substring(0, 10);
                DateTime clockDate = DateTime.ParseExact(subDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                string clockInDate = clockDate.ToString("yyyy-MM-dd");
                DataRow foundRow = dt.Rows.Find(new object[] { employeeNum, resID, opCode, clockInDate, clockIn, clockOut });
                if (foundRow == null)
                {
                    decimal[] minutes = new decimal[1440];
                    for (int i = clockIn + 1; i <= clockOut; i++)
                    {
                        minutes[i - 1] = 1m;
                    }
                    dt.Rows.Add(employeeNum, resID, opCode, clockInDate, clockIn, clockOut, minutes, 0m, 0m);
                }
                modTimes.Add(employeeNum, clockInDate, clockIn, clockOut);
                if (!isNoFGF)
                    fgfTimes.Add(resID, clockInDate, clockIn, clockOut);

                if (nextDay > 0)
                {
                    if (nextDay > 1440)
                    {
                        nextDay = 1440;
                    }

                    clockDate = clockDate.AddDays(1);
                    string nextDate = clockDate.ToString("yyyy-MM-dd");
                    DataRow foundRow2 = dt.Rows.Find(new object[] { employeeNum, resID, opCode, nextDate, 1, nextDay });
                    if (foundRow2 == null)
                    {
                        decimal[] nextMinutes = new decimal[1440];
                        for (int i = 2; i <= nextDay; i++)
                        {
                            nextMinutes[i - 1] = 1m;
                        }
                        dt.Rows.Add(employeeNum, resID, opCode, nextDate, 1, nextDay, nextMinutes, 0m, 0m);
                    }
                    modTimes.Add(employeeNum, nextDate, 1, nextDay);
                    if (!isNoFGF)
                        fgfTimes.Add(resID, nextDate, 1, nextDay);
                }
            }
            SplitTime(ref dt, modTimes, fgfTimes);
            if (dsMiscAction.Tables.Contains("ValidationJobBatch"))
            {
                DataSet ds = new DataSet("MiscAction");
                ds.Tables.Add(dt);
                ds.AcceptChanges();
                dsMiscAction.Merge(ds, true, MissingSchemaAction.Ignore);
            }
            else
            {
                dsMiscAction.Tables.Add(dt);
            }
            dsMiscAction.AcceptChanges();
            GroupTime(dt, ref dsMiscAction);
        }
        private void SplitTime(ref DataTable dt, TimeCumulator modTimes, TimeCumulator fgfTimes)
        {
            foreach (DataRow row in dt.Rows)
            {
                string employeeNum = row["EmployeeNum"].ToString();
                string resID = row["ResourceID"].ToString();
                string clockDate = row["ClockDate"].ToString();
                int clockIn = int.Parse(row["ClockIn"].ToString());
                int clockOut = int.Parse(row["ClockOut"].ToString());
                decimal[] minutes = (decimal[])row["Minutes"];
                decimal[] modMinutes = modTimes.Get(employeeNum, clockDate);
                decimal[] fgfMinutes = fgfTimes.Get(resID, clockDate);
                decimal mod = 0m;
                decimal fgf = 0m;
                for (int i = clockIn + 1; i <= clockOut; i++)
                {
                    mod += modMinutes[i - 1] > 0m ? minutes[i - 1] / modMinutes[i - 1] : 0m;
                    fgf += fgfMinutes[i - 1] > 0m ? minutes[i - 1] / fgfMinutes[i - 1] : 0m;
                }
                row["MOD"] = Math.Round(mod / 60m, 2);
                row["FGF"] = Math.Round(fgf / 60m, 2);
            }
        }
        private void GroupTime(DataTable dt, ref DataSet dsMiscAction)
        {
            var groups = from row in dt.AsEnumerable()
                         group row by new
                         {
                             ResourceID = row.Field<string>("ResourceID"),
                             OpCode = row.Field<string>("OpCode")
                         } into grp
                         select new
                         {
                             grp.Key.ResourceID,
                             grp.Key.OpCode,
                             MOD = grp.Sum(r => r.Field<Decimal>("MOD")),
                             FGF = grp.Sum(r => r.Field<Decimal>("FGF"))
                         };
            if (!dsMiscAction.Tables.Contains("TempsRepartir"))
            {
                throw new BLException("La table TempsRepartir est manquante.");
            }
            DataTable dtGroup = dsMiscAction.Tables["TempsRepartir"].Clone();
            foreach (var group in groups)
            {
                dtGroup.Rows.Add(group.ResourceID, group.OpCode, group.MOD, group.FGF);
            }
            if (dsMiscAction.Tables.Contains("TempsRepartir"))
            {
                DataSet ds = new DataSet("MiscAction");
                ds.Tables.Add(dtGroup);
                ds.AcceptChanges();
                dsMiscAction.Merge(ds, true, MissingSchemaAction.Ignore);
            }
            else
            {
                dsMiscAction.Tables.Add(dtGroup);
            }
            dsMiscAction.AcceptChanges();
        }
    }
}

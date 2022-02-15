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
    class JobBatchMODAssemblage : BaseJobBatchMOD, IJobBatchMOD
    {
        public JobBatchMODAssemblage(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session) { }
        public DataTable[] GetDataTable()
        {
            DataTable dt = new DataTable("ValidationJobBatch");
            dt.Locale = CultureInfo.InvariantCulture;
            dt.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("LaborHedSeq", typeof(string)),
                new DataColumn("LaborDtlSeq", typeof(string)),
                new DataColumn("LaborType", typeof(string)),
                new DataColumn("EmployeeNum", typeof(string)),
                new DataColumn("ResourceID", typeof(string)),
                new DataColumn("ClockDate", typeof(string)),
                new DataColumn("JobNum", typeof(string)),
                new DataColumn("OpCode", typeof(string)),
                new DataColumn("IDLigneProduction", typeof(string)),
                new DataColumn("Heures", typeof(decimal)),
                new DataColumn("FGF", typeof(decimal))
            });
            dt.PrimaryKey = new DataColumn[]
            {
                dt.Columns["LaborHedSeq"],
                dt.Columns["LaborDtlSeq"],
                dt.Columns["EmployeeNum"],
                dt.Columns["ResourceID"],
                dt.Columns["OpCode"]
            };

            DataTable dt2 = new DataTable("TempsRepartir");
            dt2.Locale = CultureInfo.InvariantCulture;
            dt2.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("EmployeeNum", typeof(string)),
                new DataColumn("OpCode", typeof(string)),
                new DataColumn("Type", typeof(string)),
                new DataColumn("Heures", typeof(decimal))
            });
            dt2.PrimaryKey = new DataColumn[]
            {
                dt2.Columns["EmployeeNum"],
                dt2.Columns["OpCode"],
                dt2.Columns["Type"]
            };

            DataTable dt3 = new DataTable("TempsMachine");
            dt3.Locale = CultureInfo.InvariantCulture;
            dt3.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("ResourceID", typeof(string)),
                new DataColumn("Heures", typeof(decimal))
            });
            dt3.PrimaryKey = new DataColumn[]
            {
                dt2.Columns["ResourceID"]
            };

            return new DataTable[] { dt, dt2, dt3 };
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
            GroupEntries modTimes = new GroupEntries();
            GroupEntries fgfTimes = new GroupEntries();
            foreach (DataRow row in dsJobBatch.Tables["LaborDtl"].Rows)
            {
                string laborHedSeq = row["LaborHedSeq"].ToString();
                string laborDtlSeq = row["LaborDtlSeq"].ToString();
                string laborType = row["LaborType"].ToString();
                string idLigneProd = row["UD_IDLigneProduction_c"].ToString();
                string laborRef = string.Format("{0}-{1}", laborHedSeq, laborDtlSeq);
                string employeeNum = row["EmployeeNum"].ToString();
                string resID = row["ResourceID"].ToString();
                string opCode = row["OpCode"].ToString();
                string jobNum = row["JobNum"].ToString();
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
                DataRow foundRow = dt.Rows.Find(new object[] { laborHedSeq, laborDtlSeq, employeeNum, resID, opCode });
                if (foundRow == null)
                {
                    decimal[] minutes = new decimal[1440];
                    for (int i = clockIn + 1; i <= clockOut; i++)
                    {
                        minutes[i - 1] = 1m;
                    }
                    dt.Rows.Add(laborHedSeq, laborDtlSeq, laborType, employeeNum, resID, clockInDate, jobNum, opCode, idLigneProd, 0m, 0m);
                }
                modTimes.Add(employeeNum, laborRef, opCode, clockInDate, clockIn, clockOut);
                if (!isNoFGF)
                    fgfTimes.Add(resID, laborRef, opCode, clockInDate, clockIn, clockOut);

                if (nextDay > 0)
                {
                    if (nextDay > 1440)
                    {
                        nextDay = 1440;
                    }

                    clockDate = clockDate.AddDays(1);
                    string nextDate = clockDate.ToString("yyyy-MM-dd");
                    DataRow foundRow2 = dt.Rows.Find(new object[] { laborHedSeq, laborDtlSeq, employeeNum, resID, opCode });
                    if (foundRow2 == null)
                    {
                        decimal[] nextMinutes = new decimal[1440];
                        for (int i = 2; i <= nextDay; i++)
                        {
                            nextMinutes[i - 1] = 1m;
                        }
                        dt.Rows.Add(laborHedSeq, laborDtlSeq, laborType, employeeNum, resID, clockInDate, jobNum, opCode, idLigneProd, nextDate, 0m, 0m);
                    }
                    modTimes.Add(employeeNum, laborRef, opCode, nextDate, 1, nextDay);
                    if (!isNoFGF)
                        fgfTimes.Add(resID, laborRef, opCode, nextDate, 1, nextDay);
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
        private void SplitTime(ref DataTable dt, GroupEntries modTimes, GroupEntries fgfTimes)
        {
            modTimes.SplitTime();
            fgfTimes.SplitTime();
            foreach (DataRow row in dt.Rows)
            {
                string employeeNum = row["EmployeeNum"].ToString();
                string resID = row["ResourceID"].ToString();
                string laborHedSeq = row["LaborHedSeq"].ToString();
                string laborDtlSeq = row["LaborDtlSeq"].ToString();
                string laborRef = string.Format("{0}-{1}", laborHedSeq, laborDtlSeq);
                decimal mod = modTimes.GetItem(employeeNum, laborRef);
                decimal fgf = fgfTimes.GetItem(resID, laborRef);
                row["Heures"] = Math.Round(mod / 60m, 2);
                row["FGF"] = Math.Round(fgf / 60m, 2);
            }
        }
        private void GroupTime(DataTable dt, ref DataSet dsMiscAction)
        {
            GroupMODTime(dt, ref dsMiscAction);
            GroupFGFTime(dt, ref dsMiscAction);
        }
        private void GroupMODTime(DataTable dt, ref DataSet dsMiscAction)
        {
            var groups = from row in dt.AsEnumerable()
                         group row by new
                         {
                             EmployeeNum = row.Field<string>("EmployeeNum"),
                             OpCode = row.Field<string>("OpCode"),
                             LaborType = row.Field<string>("LaborType")
                         } into grp
                         select new
                         {
                             grp.Key.EmployeeNum,
                             grp.Key.OpCode,
                             grp.Key.LaborType,
                             Heures = grp.Sum(r => r.Field<Decimal>("Heures"))
                         };
            if (!dsMiscAction.Tables.Contains("TempsRepartir"))
            {
                throw new BLException("La table TempsRepartir est manquante.");
            }
            DataTable dtGroup = dsMiscAction.Tables["TempsRepartir"].Clone();
            foreach (var group in groups)
            {
                string laborType = group.LaborType == "P" ? "Production" : group.LaborType == "S" ? "Setup" : "N/D";
                dtGroup.Rows.Add(group.EmployeeNum, group.OpCode, laborType, group.Heures);
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
        private void GroupFGFTime(DataTable dt, ref DataSet dsMiscAction)
        {
            var groups = from row in dt.AsEnumerable()
                         group row by new
                         {
                             ResourceID = row.Field<string>("ResourceID")
                         } into grp
                         select new
                         {
                             grp.Key.ResourceID,
                             FGF = grp.Sum(r => r.Field<Decimal>("FGF"))
                         };
            if (!dsMiscAction.Tables.Contains("TempsMachine"))
            {
                throw new BLException("La table TempsMachine est manquante.");
            }
            DataTable dtGroup = dsMiscAction.Tables["TempsMachine"].Clone();
            foreach (var group in groups)
            {
                if (string.IsNullOrEmpty(group.ResourceID))
                    continue;
                dtGroup.Rows.Add(group.ResourceID, group.FGF);
            }
            if (dsMiscAction.Tables.Contains("TempsMachine"))
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

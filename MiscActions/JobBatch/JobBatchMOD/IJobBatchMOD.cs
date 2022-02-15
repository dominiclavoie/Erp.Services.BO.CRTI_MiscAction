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
    interface IJobBatchMOD
    {
        DataTable[] GetDataTable();
        void ProcessLaborDtl(DataSet dsJobBatch, Erp.BO.CRTI_MiscAction.OperationInteraction operationInteraction, ref DataSet dsMiscAction);
    }
    abstract class BaseJobBatchMOD
    {
        protected Erp.ErpContext Db;
        protected Epicor.Hosting.Session Session;
        public BaseJobBatchMOD(Erp.ErpContext db, Epicor.Hosting.Session session)
        {
            this.Db = db;
            this.Session = session;
        }
    }
    public class GroupEntries
    {
        // EmpID/ResID => DateEntries
        private Dictionary<string, DateEntries> items;
        public GroupEntries()
        {
            items = new Dictionary<string, DateEntries>();
        }
        public void Add(string key, string laborEntryKey, string opCode, string clockDate, int clockIn, int clockOut)
        {
            if (!items.ContainsKey(key))
            {
                items[key] = new DateEntries();
            }
            items[key].Add(clockDate, laborEntryKey, opCode, clockIn, clockOut);
        }
        public void SplitTime()
        {
            foreach (KeyValuePair<string, DateEntries> item in items)
            {
                item.Value.SplitTime();
            }
        }
        public decimal GetItem(string key, string laborEntryKey)
        {
            if (items.ContainsKey(key))
            {
                return items[key].GetItem(laborEntryKey);
            }
            return 0m;
        }
    }
    public class DateEntries
    {
        // ClockDate => DateEntry
        private Dictionary<string, DateEntry> items;
        public DateEntries()
        {
            items = new Dictionary<string, DateEntry>();
        }
        public void Add(string key, string laborEntryKey, string opCode, int clockIn, int clockOut)
        {
            if (!items.ContainsKey(key))
            {
                items[key] = new DateEntry();
            }
            items[key].Add(laborEntryKey, opCode, clockIn, clockOut);
        }
        public void SplitTime()
        {
            foreach(KeyValuePair<string, DateEntry> item in items)
            {
                item.Value.SplitTime();
            }
        }
        public decimal GetItem(string laborEntryKey)
        {
            decimal minutes = 0m;
            foreach (KeyValuePair<string, DateEntry> item in items)
            {
                minutes += item.Value.GetItem(laborEntryKey);
            }
            return minutes;
        }
    }
    public class DateEntry
    {
        private decimal minutes;
        private decimal[] timeArray;
        private List<TimeEntry> timeEntries;
        public DateEntry()
        {
            minutes = -1m;
            timeArray = new decimal[1440];
            timeEntries = new List<TimeEntry>();
        }
        public void Add(string key, string opCode, int clockIn, int clockOut)
        {
            TimeEntry te = new TimeEntry(key, opCode, clockIn, clockOut);
            te.BuildTimeArray(ref timeArray);
            timeEntries.Add(te);
        }
        public void SplitTime()
        {
            minutes = 0m;
            foreach(TimeEntry te in timeEntries)
            {
                te.SplitTime(timeArray);
                minutes += te.SplitMinutes;
            }
        }
        public decimal GetItem(string laborEntryKey)
        {
            decimal min = 0m;
            foreach (TimeEntry te in timeEntries)
            {
                if(te.LaborEntryKey == laborEntryKey)
                {
                    min += te.SplitMinutes;
                }
            }
            return min;
        }
    }
    public class TimeEntry
    {
        private string laborEntryKey;
        private string opCode;
        private int clockIn;
        private int clockOut;
        private decimal minutes;
        private decimal splitMinutes;
        private decimal[] timeArray;
        public decimal SplitMinutes { get => splitMinutes; }
        public string OpCode { get => opCode; }
        public string LaborEntryKey { get => laborEntryKey; }

        public TimeEntry(string _laborEntryKey, string _opCode, int _clockIn, int _clockOut)
        {
            laborEntryKey = _laborEntryKey;
            opCode = _opCode;
            clockIn = _clockIn;
            clockOut = _clockOut;
            minutes = (decimal)clockOut - (decimal)clockIn;
            splitMinutes = -1m;
            timeArray = new decimal[1440];
        }
        public void BuildTimeArray(ref decimal[] dateTimeArray)
        {
            for (int i = clockIn + 1; i <= clockOut; i++)
            {
                timeArray[i - 1] += 1m;
                dateTimeArray[i - 1] += 1m;
            }
        }
        public void SplitTime(decimal[] dateTimeArray)
        {
            splitMinutes = 0m;
            for (int i = clockIn + 1; i <= clockOut; i++)
            {
                splitMinutes += timeArray[i - 1] / dateTimeArray[i - 1];
            }
        }
    }

    public class TimeCumulator
    {
        private Dictionary<string, DateTimeCumulator> items;
        public TimeCumulator()
        {
            items = new Dictionary<string, DateTimeCumulator>();
        }
        public void Add(string key, string clockDate, int clockIn, int clockOut)
        {
            if (!items.ContainsKey(key))
            {
                items[key] = new DateTimeCumulator();
            }
            items[key].Add(clockDate, clockIn, clockOut);
        }
        public decimal[] Get(string key, string clockDate)
        {
            if (!items.ContainsKey(key))
            {
                //throw new KeyNotFoundException();
                return new decimal[1440];
            }
            return items[key].Get(clockDate);
        }
    }
    public class DateTimeCumulator
    {
        private Dictionary<string, decimal[]> items;
        public DateTimeCumulator()
        {
            items = new Dictionary<string, decimal[]>();
        }
        public void Add(string key, int clockIn, int clockOut)
        {
            if (!items.ContainsKey(key))
            {
                items[key] = new decimal[1440];
            }
            for (int i = clockIn + 1; i <= clockOut; i++)
            {
                items[key][i - 1] += 1m;
            }
        }
        public decimal[] Get(string key)
        {
            if (!items.ContainsKey(key))
            {
                //throw new KeyNotFoundException();
                return new decimal[1440];
            }
            return items[key];
        }
    }
    // BACKUP SPLIT GENERAL****************************
    /*
    public class TimeCumulator
    {
        private Dictionary<string, DateTimeCumulator> items;
        public TimeCumulator()
        {
            items = new Dictionary<string, DateTimeCumulator>();
        }
        public void Add(string key, string clockDate, int clockIn, int clockOut)
        {
            if (!items.ContainsKey(key))
            {
                items[key] = new DateTimeCumulator();
            }
            items[key].Add(clockDate, clockIn, clockOut);
        }
        public decimal[] Get(string key, string clockDate)
        {
            if (!items.ContainsKey(key))
            {
                //throw new KeyNotFoundException();
                return new decimal[1440];
            }
            return items[key].Get(clockDate);
        }
    }
    public class DateTimeCumulator
    {
        private Dictionary<string, decimal[]> items;
        public DateTimeCumulator()
        {
            items = new Dictionary<string, decimal[]>();
        }
        public void Add(string key, int clockIn, int clockOut)
        {
            if (!items.ContainsKey(key))
            {
                items[key] = new decimal[1440];
            }
            for (int i = clockIn + 1; i <= clockOut; i++)
            {
                items[key][i - 1] += 1m;
            }
        }
        public decimal[] Get(string key)
        {
            if (!items.ContainsKey(key))
            {
                //throw new KeyNotFoundException();
                return new decimal[1440];
            }
            return items[key];
        }
    }
    */
}

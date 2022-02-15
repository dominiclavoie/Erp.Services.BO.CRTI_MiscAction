using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Erp.BO.CRTI_MiscAction.MiscActions.GestionSuiviHeures;

namespace Erp.BO.CRTI_MiscAction
{
    class GestionSuiviHeures : BaseMiscAction, IMiscAction
    {
        public GestionSuiviHeures(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session) {}

        public override DataTable[] GetDataTable()
        {
            DataTable dt = new DataTable("SuiviHeures");
            dt.Locale = CultureInfo.InvariantCulture;
            dt.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("EmployeeNum", typeof(string)),
                new DataColumn("OpCode", typeof(string)),
                new DataColumn("DateStart", typeof(DateTime)),
				new DataColumn("DateEnd", typeof(DateTime)),
				new DataColumn("WeekNumber", typeof(string)),
				new DataColumn("Sunday", typeof(decimal)),
				new DataColumn("Monday", typeof(decimal)),
				new DataColumn("Tuesday", typeof(decimal)),
				new DataColumn("Wednesday", typeof(decimal)),
				new DataColumn("Thursday", typeof(decimal)),
				new DataColumn("Friday", typeof(decimal)),
				new DataColumn("Saturday", typeof(decimal)),
				new DataColumn("SysRowID", typeof(Guid))
            });
            
            return new DataTable[] { dt };
        }

		private LaborHoursCollection GetLaborHoursCollection(int year)
        {
			
			DayCollection dayCollection = new DayCollection(year);
			IEnumerable<LaborHours> lbr = (from dy in dayCollection.Days
					   join em in this.Db.EmpBasic on new { Company = this.Session.CompanyID }
											   equals new { em.Company }
					   join lb in this.Db.LaborDtl on new { em.Company, EmployeeNum = em.EmpID, ClockDate = dy.Date }
											   equals new { lb.Company, lb.EmployeeNum, ClockDate = lb.ClockInDate } into labors
					   from lb in labors.DefaultIfEmpty()
					   select new
					   {
						   em.EmpID,
						   Date = dy.Date ?? DateTime.Now,
						   dy.DateStart,
						   dy.DateEnd,
						   dy.WeekNumber,
						   OpCode = lb == null ? "" : lb.OpCode,
						   Sunday = dy.DayOfWeek == DayOfWeek.Sunday ? lb == null ? 0m : lb.LaborHrs : 0m,
						   Monday = dy.DayOfWeek == DayOfWeek.Monday ? lb == null ? 0m : lb.LaborHrs : 0m,
						   Tuesday = dy.DayOfWeek == DayOfWeek.Tuesday ? lb == null ? 0m : lb.LaborHrs : 0m,
						   Wednesday = dy.DayOfWeek == DayOfWeek.Wednesday ? lb == null ? 0m : lb.LaborHrs : 0m,
						   Thursday = dy.DayOfWeek == DayOfWeek.Thursday ? lb == null ? 0m : lb.LaborHrs : 0m,
						   Friday = dy.DayOfWeek == DayOfWeek.Friday ? lb == null ? 0m : lb.LaborHrs : 0m,
						   Saturday = dy.DayOfWeek == DayOfWeek.Saturday ? lb == null ? 0m : lb.LaborHrs : 0m
					   }).GroupBy( g => new
                       {
						   g.EmpID,
						   g.WeekNumber,
						   g.DateStart,
						   g.DateEnd,
						   g.OpCode
                       }).Select( tt => new LaborHours
                       {
						   EmpID = tt.Key.EmpID,
						   DateStart = tt.Key.DateStart,
						   DateEnd = tt.Key.DateEnd,
						   WeekNumber = tt.Key.WeekNumber,
						   OpCode = tt.Key.OpCode,
						   Sunday = tt.Sum( s => s.Sunday ),
						   Monday = tt.Sum(s => s.Monday),
						   Tuesday = tt.Sum(s => s.Tuesday),
						   Wednesday = tt.Sum(s => s.Wednesday),
						   Thursday = tt.Sum(s => s.Thursday),
						   Friday = tt.Sum(s => s.Friday),
						   Saturday = tt.Sum(s => s.Saturday),
						   RowID = Guid.NewGuid()
					   });
			return new LaborHoursCollection(lbr);
		}

		private void GetLaborHours(int year, string laborHoursType, DateTime date)
        {
			LaborHoursCollection laborHoursCollection = GetLaborHoursCollection(year);
			DataTable dtSuiviHeures = GetDataTable("SuiviHeures");
			foreach (LaborHours laborHours in laborHoursCollection.GetLaborHours(laborHoursType, date))
			{
				dtSuiviHeures.Rows.Add(laborHours.GetValues());
			}
			MergeDataTable(dtSuiviHeures, true);
		}

		public DataSet GetLaborHours(DataSet iDataSet, int year, string laborHoursType, DateTime date)
        {
			LoadDataSet(iDataSet);
			GetLaborHours(year, laborHoursType, date);
			return this.dsMiscAction;
		}

	}
}

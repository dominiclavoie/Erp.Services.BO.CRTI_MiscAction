using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Erp.BO.CRTI_MiscAction.MiscActions.GestionSuiviHeures
{
    public class Day
    {
		public DateTime? Date { get; set; }
		public int WeekNumber { get; set; }
		public DayOfWeek DayOfWeek { get; set; }
		public DateTime DateStart { get; set; }
		public DateTime DateEnd { get; set; }
		public Day(DateTime _date, int _weekNumber, DateTime _dateStart, DateTime _dateEnd)
		{
			Date = _date;
			WeekNumber = _weekNumber;
			DayOfWeek = _date.DayOfWeek;
			DateStart = _dateStart;
			DateEnd = _dateEnd;
		}
	}
}

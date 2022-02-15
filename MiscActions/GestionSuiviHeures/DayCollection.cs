using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

namespace Erp.BO.CRTI_MiscAction.MiscActions.GestionSuiviHeures
{
    public class DayCollection
    {
		private IEnumerable<Day> _days;
		public IEnumerable<Day> Days { get { return _days; } }
		public DayCollection(int year)
		{
			GetYearDays(year);
		}
		private enum WeekRule
		{
			FirstFourDayWeek,
			FirstFullWeek
		}

		public static DateTime GetFirstWeekOfMonthDate(int year, int month)
        {
			WeekRule weekRule = WeekRule.FirstFourDayWeek;
			System.DayOfWeek firstDayOfWeekResolved = DateTimeFormatInfo.CurrentInfo.FirstDayOfWeek;
			DateTime result = new DateTime(year, month, 1);
			int num = result.DayOfWeek - firstDayOfWeekResolved;
			try
			{
				switch (weekRule)
				{
					case WeekRule.FirstFourDayWeek:
						if (num > 3)
						{
							result = result.AddDays(7 - num);
							return result;
						}
						if (num < -3)
						{
							result = result.AddDays(-(7 + num));
							return result;
						}
						result = result.AddDays(-num);
						return result;
					case WeekRule.FirstFullWeek:
						if (num > 0)
						{
							result = result.AddDays(7 - num);
							return result;
						}
						if (num < 0)
						{
							result = result.AddDays(-num);
							return result;
						}
						return result;
					default:
						if (num < 0)
						{
							result = result.AddDays(-(7 + num));
							return result;
						}
						result = result.AddDays(-num);
						return result;
				}
			}
			catch
			{
				if (year == 1 && month == 1)
				{
					return new DateTime(1, 1, 1);
				}
				return result;
			}
		}

		private DateTime GetFirstWeekOfYearDate(int year)
		{
			return DayCollection.GetFirstWeekOfMonthDate(year, 1);
		}

		private void GetYearDays(int year)
		{
			List<Day> days = new List<Day>();
			DateTime firstWeekOfYearDate = GetFirstWeekOfYearDate(year);
			DateTime lastWeekOfYearDate = GetFirstWeekOfYearDate(year + 1);
			DateTime date = firstWeekOfYearDate.AddDays(0);
			DateTime dateStart = date.AddDays(0);
			DateTime dateEnd = date.AddDays(6);
			int currentWeek = 1;
			while (date < lastWeekOfYearDate)
			{
				int w = date.Subtract(firstWeekOfYearDate).Days / 7 + 1;
				if (w > currentWeek)
				{
					currentWeek++;
					dateStart = dateStart.AddDays(7);
					dateEnd = dateEnd.AddDays(7);
				}
				days.Add(new Day(date, w, dateStart, dateEnd));
				date = date.AddDays(1);

			}
			_days = days.AsEnumerable();
		}
	}
}

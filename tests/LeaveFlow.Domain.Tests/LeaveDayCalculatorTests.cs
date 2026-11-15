using LeaveFlow.Domain;

namespace LeaveFlow.Domain.Tests;

public class LeaveDayCalculatorTests
{
    private static readonly HashSet<DateOnly> NoHolidays = [];
    private static DateOnly D(int month, int day) => new(2026, month, day);

    [Fact]
    public void MondayToFriday_IsFiveDays() =>
        Assert.Equal(5m, LeaveDayCalculator.WorkingDays(D(11, 16), D(11, 20), HalfDay.None, NoHolidays));

    [Fact]
    public void FridayToMonday_SkipsWeekend() =>
        Assert.Equal(2m, LeaveDayCalculator.WorkingDays(D(11, 20), D(11, 23), HalfDay.None, NoHolidays));

    [Fact]
    public void Holiday_IsExcluded()
    {
        var holidays = new HashSet<DateOnly> { D(12, 7) }; // Mon, ชดเชยวันพ่อ
        Assert.Equal(4m, LeaveDayCalculator.WorkingDays(D(12, 7), D(12, 11), HalfDay.None, holidays));
    }

    [Fact]
    public void SingleDay_IsOne() =>
        Assert.Equal(1m, LeaveDayCalculator.WorkingDays(D(11, 18), D(11, 18), HalfDay.None, NoHolidays));

    [Theory]
    [InlineData(HalfDay.Morning)]
    [InlineData(HalfDay.Afternoon)]
    public void HalfDay_IsHalf(HalfDay half) =>
        Assert.Equal(0.5m, LeaveDayCalculator.WorkingDays(D(11, 18), D(11, 18), half, NoHolidays));

    [Fact]
    public void HalfDayOnWeekend_IsZero() =>
        Assert.Equal(0m, LeaveDayCalculator.WorkingDays(D(11, 21), D(11, 21), HalfDay.Morning, NoHolidays));

    [Fact]
    public void AllWeekend_IsZero() =>
        Assert.Equal(0m, LeaveDayCalculator.WorkingDays(D(11, 21), D(11, 22), HalfDay.None, NoHolidays));

    [Fact]
    public void EndBeforeStart_Throws()
    {
        var ex = Assert.Throws<DomainException>(() =>
            LeaveDayCalculator.WorkingDays(D(11, 20), D(11, 16), HalfDay.None, NoHolidays));
        Assert.Contains("วันสิ้นสุด", ex.Message);
    }

    [Fact]
    public void HalfDayOverMultipleDays_Throws() =>
        Assert.Throws<DomainException>(() =>
            LeaveDayCalculator.WorkingDays(D(11, 16), D(11, 17), HalfDay.Morning, NoHolidays));

    [Fact]
    public void CrossYear_Throws()
    {
        var ex = Assert.Throws<DomainException>(() =>
            LeaveDayCalculator.WorkingDays(new DateOnly(2026, 12, 30), new DateOnly(2027, 1, 4), HalfDay.None, NoHolidays));
        Assert.Contains("ปี", ex.Message);
    }
}

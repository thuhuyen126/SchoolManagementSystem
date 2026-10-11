using SchoolManagementSystem.Models;
using Xunit;

namespace SchoolManagementSystem.Tests;

public sealed class NullableViewModelTests
{
    [Fact]
    public void AttendanceWithoutMatchingRecord_IsReportedAsPresent()
    {
        var model = new StudentSubjectAttendanceViewModel
        {
            Attendance = null
        };

        Assert.Equal("Present", model.Status);
        Assert.Equal(0, model.TotalAbsences);
    }

    [Fact]
    public void HomeViewModel_CollectionsDefaultToEmpty()
    {
        var model = new HomeViewModel();

        Assert.Empty(model.Courses);
        Assert.Empty(model.SchoolClasses);
    }
}

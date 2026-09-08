using StudyMate.Models;
using StudyMate.Services;
using Xunit;
namespace StudyMate.Tests;
public class SessionSchedulerTests
{
 [Fact] public void CapacityAndWeekWrapAreHandled(){Assert.True(SessionScheduler.HasCapacity(2,1));Assert.False(SessionScheduler.HasCapacity(2,2));Assert.Equal(1,SessionScheduler.DaysUntil(DayOfWeek.Monday,new DateTime(2026,9,6)));}
 [Fact] public void BlockedParticipantCannotJoin(){var s=new StudySession{Capacity=3,Participants=new List<SessionParticipant>{new(){StudentId=2}}};Assert.False(SessionScheduler.CanJoin(s,1,new[]{new Block{BlockerStudentId=2,BlockedStudentId=1}}));}
}

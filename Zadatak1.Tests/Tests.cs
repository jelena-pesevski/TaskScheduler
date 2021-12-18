using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Zadatak1.Tests
{
    [TestClass]
    public class MyScheduler
    {
        [TestMethod]
        public void ScheduleTasks()
        {
            const int numThreads = 5;
            const int numTasks = 15;
            const int oneSecondDelayInMilliseconds = 1000;

            MyLibrary.MyScheduler myScheduler = new MyLibrary.MyScheduler(numThreads, MyLibrary.MyScheduler.Mode.NonPreemptive);
            for(int i=0; i<numTasks; i++)
            {
                MyLibrary.MyScheduler.MyToken token = myScheduler.CreateMyToken();
                myScheduler.ScheduleTask(() => Task.Delay(oneSecondDelayInMilliseconds).Wait() , token, i%3, 3);

            }

            Assert.AreEqual(numThreads, myScheduler.ExecutingTasksCount);
            Assert.AreEqual(numTasks, myScheduler.CurrentTaskCount);

        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Zadatak1.Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            const int numOfThreads = 2;
            const int oneSecondDelayInMilliseconds = 1000;

            MyLibrary.MyScheduler myScheduler = new MyLibrary.MyScheduler(numOfThreads, MyLibrary.MyScheduler.Mode.Preemptive); 
                
            // prva dva zadatka simuliraju deadlock
            MyLibrary.MyScheduler.MyToken tkn = myScheduler.CreateMyToken();
            void f1()
            {
                Console.WriteLine($"Ja sam zadatak 1r i izvrsavam se ");
                MyLibrary.Resource res = MyLibrary.Resource.LockMeResource(tkn.MyTask, "FAJL1");

                if (tkn.IsCanceled) return;
                Console.WriteLine($"Ja sam zadatak 1r i dobio sam FAJL1 ");
            

                Task.Delay(oneSecondDelayInMilliseconds).Wait();


                MyLibrary.Resource res1 = MyLibrary.Resource.LockMeResource(tkn.MyTask, "FAJL2");
                if (tkn.IsCanceled) return;
                Console.WriteLine($"Ja sam zadatak 1r i dobio sam FAJL2 ");
               

                Task.Delay(oneSecondDelayInMilliseconds).Wait();
                MyLibrary.Resource.UnlockResource(tkn.MyTask, res1);
                MyLibrary.Resource.UnlockResource(tkn.MyTask, res);
              
                Console.WriteLine("Ja sam zadatak 1r i zavrsavam uspjesno ");
            }
            myScheduler.ScheduleTask(f1, tkn, 2, 7);



            MyLibrary.MyScheduler.MyToken tkn2 = myScheduler.CreateMyToken(); //dobija token za koristenje
            void f2()
            {
                Console.WriteLine($"Ja sam zadatak 2r i izvrsavam se ");
                MyLibrary.Resource res = MyLibrary.Resource.LockMeResource(tkn2.MyTask, "FAJL2");
                if (tkn2.IsCanceled) return;
                Console.WriteLine($"Ja sam zadatak 2r i dobio sam FAJL2 ");

                Task.Delay(oneSecondDelayInMilliseconds).Wait();

                MyLibrary.Resource res1 = MyLibrary.Resource.LockMeResource(tkn2.MyTask, "FAJL1");
                if (tkn2.IsCanceled) return;
        
                Console.WriteLine($"Ja sam zadatak 2r i dobio sam FAJL1 ");

                Task.Delay(oneSecondDelayInMilliseconds).Wait();

                MyLibrary.Resource.UnlockResource(tkn2.MyTask, res1);
                MyLibrary.Resource.UnlockResource(tkn2.MyTask, res);
               
                Console.WriteLine($"Ja sam zadatak 2r i zavrsavam uspjesno ");

            }
            myScheduler.ScheduleTask(f2, tkn2, 1, 8);



            MyLibrary.MyScheduler.MyToken tokenForMyFunction = myScheduler.CreateMyToken();

            void print()
            {
                MyLibrary.Resource res = MyLibrary.Resource.LockMeResource(tokenForMyFunction.MyTask, "FAJL1");
                if (tokenForMyFunction.IsCanceled) return;
         
                Task.Delay(oneSecondDelayInMilliseconds).Wait();
           
                Console.WriteLine($"Ja sam zadatak 1 i dobio sam FAJL1 ");


                MyLibrary.Resource.UnlockResource(tokenForMyFunction.MyTask, res);
                for (int i=0; i<5; i++)
                {

                    while (tokenForMyFunction.IsOnWait)
                    {
                        tokenForMyFunction.Handler.WaitOne();
                    }

                    Console.WriteLine($"Ja sam zadatak 1 i izvrsavam se {i}");
                    Task.Delay(oneSecondDelayInMilliseconds).Wait();

                    if (tokenForMyFunction.IsCanceled)
                    {
                        return;
                    }
                }
                if (tokenForMyFunction.IsCanceled)
                {
                    return;
                }
                
            }              
            myScheduler.ScheduleTask(print, tokenForMyFunction , 2, 5);


        

             MyLibrary.MyScheduler.MyToken tokenForMyFunction2 = myScheduler.CreateMyToken(); 
              void print2()
              {
                  for (int i = 0; i < 10; i++)
                  {

                      while(tokenForMyFunction2.IsOnWait)
                      {
                          tokenForMyFunction2.Handler.WaitOne();
                      }

                      Console.WriteLine($"Ja sam zadatak 2 i izvrsavam se {i}");
                      Task.Delay(oneSecondDelayInMilliseconds).Wait();

                      if (tokenForMyFunction2.IsCanceled)
                      {
                          break;
                      }
                  }

              }
              myScheduler.ScheduleTask(print2, tokenForMyFunction2,3 , 5 );


              MyLibrary.MyScheduler.MyToken tokenForMyFunction3 = myScheduler.CreateMyToken(); //dobija token za koristenje
              void print3()
              {
                  for (int i = 0; i < 10; i++)
                  {

                      while (tokenForMyFunction3.IsOnWait)
                      {
                          tokenForMyFunction3.Handler.WaitOne();
                      }

                      Console.WriteLine($"Ja sam zadatak 3 i izvrsavam se {i}");
                      Task.Delay(oneSecondDelayInMilliseconds).Wait();

                      if (tokenForMyFunction3.IsCanceled)
                      {
                          break;
                      }
                  }

              }
              myScheduler.ScheduleTask(print3, tokenForMyFunction3, 1, 5 );


              MyLibrary.MyScheduler.MyToken tokenForMyFunction4 = myScheduler.CreateMyToken(); //dobija token za koristenje
              void print4()
              {
                  for (int i = 0; i < 10; i++)
                  {

                      while (tokenForMyFunction4.IsOnWait)
                      {
                          tokenForMyFunction4.Handler.WaitOne();
                      }

                      Console.WriteLine($"Ja sam zadatak 4 i izvrsavam se {i}");
                      Task.Delay(oneSecondDelayInMilliseconds).Wait();

                      if (tokenForMyFunction4.IsCanceled)
                      {
                          break;
                      }
                  }

              }
              myScheduler.ScheduleTask(print4, tokenForMyFunction4, 4, 5 );

              myScheduler.ScheduleTask(() => { Console.WriteLine("Ja sam zadatak 5"); }, myScheduler.CreateMyToken(), 1, 4);
              myScheduler.ScheduleTask(() => { Console.WriteLine("Ja sam zadatak 6"); }, myScheduler.CreateMyToken(), 2, 5);
              myScheduler.ScheduleTask(() => { Console.WriteLine("Ja sam zadatak 7"); }, myScheduler.CreateMyToken(), 1, 6);
              myScheduler.ScheduleTask(() => { Console.WriteLine("Ja sam zadatak 8"); }, myScheduler.CreateMyToken(), 2, 2);
              myScheduler.ScheduleTask(() => { Console.WriteLine("Ja sam zadatak 9"); }, myScheduler.CreateMyToken(), 4, 2);

              while (myScheduler.CurrentTaskCount > 0)
              {
              //    Console.WriteLine("Preostalo:" + myScheduler.CurrentTaskCount);
                  Task.Delay(oneSecondDelayInMilliseconds).Wait();
              }


              Console.WriteLine("Done.");


              Console.WriteLine("Rasporedjivanje nativnim:");

              List<Task> tasks = new List<Task>();

              Object lockObj = new Object();

                //kod prva dva zadatka moze doci do deadlock-a

              MyLibrary.MyScheduler.MyToken tk1 = myScheduler.CreateMyToken();
              Task t1 = new Task(() =>
                  {

                      if (tk1.IsCanceled)
                      {
                          return;
                      }
                      lock (lockObj)
                      { 
                          Console.WriteLine($"Ja sam zadatak 1 i izvrsavam se ");
                      }
                      MyLibrary.Resource res = MyLibrary.Resource.LockMeResource(tk1.MyTask, "FAJL1");
                      if (tk1.IsCanceled) return;
                      Console.WriteLine($"Ja sam zadatak 1 i dobio sam FAJL1 ");

                      Task.Delay(oneSecondDelayInMilliseconds).Wait();


                      MyLibrary.Resource res1 = MyLibrary.Resource.LockMeResource(tk1.MyTask, "FAJL2");
                      if (tk1.IsCanceled) return;
                      Console.WriteLine($"Ja sam zadatak 1 i dobio sam FAJL2 ");


                      Task.Delay(oneSecondDelayInMilliseconds).Wait();
                      MyLibrary.Resource.UnlockResource(tk1.MyTask, res1);

                  MyLibrary.Resource.UnlockResource(tk1.MyTask, res);

              });

              myScheduler.RegisterTask(t1, tk1, 1, 5);
              tasks.Add(t1);
              t1.Start(myScheduler);


              MyLibrary.MyScheduler.MyToken tk2 = myScheduler.CreateMyToken();
              Task t2 = new Task(() =>
              {

                      if (tk2.IsCanceled)
                      {
                          return;
                      }
                      lock (lockObj)
                      {
                          Console.WriteLine($"Ja sam zadatak 2 i izvrsavam se ");
                      }

                  MyLibrary.Resource res = MyLibrary.Resource.LockMeResource(tk2.MyTask, "FAJL2");
                  if (tk2.IsCanceled) return;
                  Console.WriteLine($"Ja sam zadatak 2 i dobio sam FAJL2 ");

                  Task.Delay(oneSecondDelayInMilliseconds).Wait();


                  MyLibrary.Resource res1 = MyLibrary.Resource.LockMeResource(tk2.MyTask, "FAJL1");
                  if (tk2.IsCanceled) return;
                  Console.WriteLine($"Ja sam zadatak 2 i dobio sam FAJL1 ");


                  Task.Delay(oneSecondDelayInMilliseconds).Wait();
                  MyLibrary.Resource.UnlockResource(tk2.MyTask, res1);

                  MyLibrary.Resource.UnlockResource(tk2.MyTask, res);


                  Task.Delay(oneSecondDelayInMilliseconds).Wait();

              });

              myScheduler.RegisterTask(t2,tk2, 2,4);
              tasks.Add(t2);
              t2.Start(myScheduler);

              MyLibrary.MyScheduler.MyToken tk3 = myScheduler.CreateMyToken();
              Task t3 = new Task(() =>
              {
                  for (int i = 0; i < 5; i++)
                  {
                      if (tk3.IsCanceled)
                      {
                          break;
                      }
                      lock (lockObj)
                      {
                          Console.WriteLine($"Ja sam zadatak 3 i izvrsavam se {i} ");
                      }

                      Task.Delay(oneSecondDelayInMilliseconds).Wait();
                  }
              });

              myScheduler.RegisterTask(t3,tk3, 3,5);
              tasks.Add(t3);
              t3.Start(myScheduler);


              MyLibrary.MyScheduler.MyToken tk4 = myScheduler.CreateMyToken();
              Task t4 = new Task(() =>
              {

                      if (tk4.IsCanceled)
                      {
                          return;
                      }
                      lock (lockObj)
                      {
                          Console.WriteLine($"Ja sam zadatak 4 i izvrsavam se ");
                      }


                      MyLibrary.Resource res = MyLibrary.Resource.LockMeResource(tk4.MyTask, "FAJL1");
                      if (tk4.IsCanceled) return;
                      Console.WriteLine($"Ja sam zadatak 4 i dobio sam FAJL1 ");

                      Task.Delay(oneSecondDelayInMilliseconds).Wait();

                      MyLibrary.Resource.UnlockResource(tk4.MyTask, res);

                      Task.Delay(oneSecondDelayInMilliseconds).Wait();

              });

              myScheduler.RegisterTask(t4, tk4,2, 4);
              tasks.Add(t4);
              t4.Start(myScheduler);

              Task.WaitAll(tasks.ToArray());
              Console.WriteLine("Done.");
        }
    }
}

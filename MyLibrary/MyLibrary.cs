using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyLibrary
{
    /// <summary>
    /// MyScheduler je glavna klasa koja predstavlja rasporedjivac zadataka. 
    /// Sadrzi sve metode potrebne za rasporedjivanje.
    /// </summary>
    public class MyScheduler : TaskScheduler
    {
        /// <summary>
        /// Moguci modovi rada rasporedjivaca
        /// </summary>
        public enum Mode{
            Preemptive,
            NonPreemptive
        }

        // mod rada
        private Mode _mode;

        // broj niti sa kojima rasporedjivac raspolaze, maksimalan broj paralelnih tokova.
        private readonly int _maxDegreeOfParallelism;

        // objekti za sinhronizaciju
        private readonly object locker = new object();

        private readonly object lockForPriority = new object();

        // delegate koji oznacava oblik u kome korisnik salje svoje funkcije
        public delegate void FunctionForExecution();

        /// <summary>
        /// Klasa koja predstavalja token, koji omogucava kooperativno upravljanje zadacima koji se rasporedjuju.
        /// </summary>
        public class MyToken
        {
            /// <summary>
            /// Property pomocu kojeg korisnikova funkcija provjerava da li je stavljena na cekanje.
            /// </summary>
            public bool IsOnWait { get; private set; }

            // zadatak se postavlja na cekanje
            internal void SetWait() => IsOnWait = true;

            // zadatak vise nije na cekanju
            internal void ResetWait() => IsOnWait = false;

            /// <summary>
            /// Property pomocu kojeg korisnikova funkcija provjerava da li je otkazana.
            /// </summary>
            public bool IsCanceled { get; private set; }

            // funkcija kojom se okoncava izvrsavanje korisnikove funkcije
            internal void Cancel() => IsCanceled = true;

            /// <summary>
            /// Polje pomocu kojeg se korisnikova funkcija blokira.
            /// </summary>
            public EventWaitHandle Handler = new EventWaitHandle(false, EventResetMode.AutoReset);

            /// <summary>
            /// Property koji oznacava zadatak kome dati token pripada.
            /// </summary>
            public Task MyTask { get; internal set; }
        }

        /// <summary>
        /// Funkcija koju korisnik poziva da bi dobio token koji ce koristiti u svojoj funkciji.
        /// Taj token ce biti pridruzen zadatku kreiranom od te funkcije, te ce omogucavati kooperativno zaustavljanje i upravljanje korisinckom funckijom.
        /// </summary>
        /// <returns>
        /// novokreirani token
        /// </returns>
        public MyToken CreateMyToken()
        {
            return new MyToken();
        }


        // Klasa koja se koristi za cuvanje svih potrebnih informacija o zadacima koji se rasporedjuju.
        internal class MetadataForTask
        {
            internal MetadataForTask(MyToken token, int priority, int maxDuration, Task myTask)
            {
                Priority = priority;
                MaxDuration = maxDuration;
                Token = token;
                Working = false;
                TaskWithCallback = null; //polje koje se koristi za preemptive
                Token.MyTask = myTask;
            }

            // prioritet zadatka
            internal int Priority { get; set; }

            // maksimalno predvidjeno vrijeme izvrsavanja
            internal int MaxDuration { get; private set; }

            // zadatak koji obezbjedjuje da se zadatak zavrsi nakon sto istekne maksimalno predvidjeno vrijeme izvrsavanja
            internal Task TaskWithCallback { get; set; }

            // interni fleg, koji oznacava da li se zadatak trenutno izvrsava (znaci da nije prekinut)
            internal bool Working { get; set; }

            // stoperica koja mjeri vrijeme izvrsavanja zadatka
            internal Stopwatch stopwatch = new Stopwatch();

            // token dodijeljen zadatku
            internal MyToken Token { get; set; }

            // fleg koji oznacava da li je prioritet taska promijenjen (prilikom rada sa resursima)
            internal bool IsPriorityChanged { get; set; }

            // funkcija kojom se zadatku postavlja najveci prioritet kada radi sa resursima
            internal void SetMaximumPriority()
            {
                PreviousPriority = Priority;
                IsPriorityChanged = true;
                Priority = 1;
            }

            // funkcija pomocu koje se prioritet vraca na vrijednost koju je dodijelio korisnik
            internal void ReturnPreviousPriority()
            {
                Priority = PreviousPriority;
                IsPriorityChanged = false;
            }

            // property koji cuva vrijednost dodijeljenu od strane korisnika
            internal int PreviousPriority { get; set; }
  
        }

        // redefinicija komparatora, koja omogucava da se u SortedList pojavljuju duplikati, koji ce se pri izvrsavanju tretirati po FIFO redoslijedu
        internal class MyKeyComparer<TKey> : IComparer<TKey> where TKey : IComparable
        {
            public int Compare(TKey x, TKey y)
            {
                int result = x.CompareTo(y);

                if (result == 0)
                    return -1;   
                else
                    return result;
            }
        }

        /// <summary>
        /// Property koji oznacava trenutan broj zadataka koji se rasporedjuju, zajedno sa onima koji su na cekanju, ili jos nikako nisu rasporedjeni.
        /// </summary>
        public int CurrentTaskCount => pendingTasks.Count;

        /// <summary>
        /// Property koji oznacava trenutan broj zadataka koji se aktivno izvrsavaju.
        /// </summary>
        public int ExecutingTasksCount => executingTasks.Length;

        // kolekcija koja predstavalja mapiranje zadatka u njegove podatke
        internal readonly Dictionary<Task, MetadataForTask> database = new Dictionary<Task, MetadataForTask>(); 

        // kolekcija koja predstavlja PriorityQueue, zadaci su sortirani po prioritetu, oni sa istim prioritetom su u FIFO poretku
        internal readonly SortedList<int, Task> pendingTasks = new SortedList<int, Task>(new MyKeyComparer<int>());

        // kolekcija koja predstavlja zadatke koji se trenutno izvrsavaju
        internal readonly Task [] executingTasks;

        /// <summary>
        /// Funkcija kojom korisnik prosljedjuje rasporedjivacu funkciju koju zeli da izvrsi, zajedno sa tokenom koji ta funkcija koristi,
        /// prioritetom i maksimalnim predvidjenim trajanjem izvrsavanja. Na ovaj nacin se registruje korisnikov zadatak, i potencijalno pokrece njegovo izvrsavanje.
        /// </summary>
        /// <param name="func"> Funkcija koja ce se izvrsavati.</param>
        /// <param name="token"> Token koji ta funkcija koristi za kooperativno zaustavljanje.</param>
        /// <param name="priority"> Prioritet funkcije pri izvrsavanju. (manja vrijednost znaci veci prioritet)</param>
        /// <param name="maxDuration"> Maksimalno predvidjeno vrijeme trajanja izvrsavanja.</param>
        public void ScheduleTask(FunctionForExecution func, MyToken token, int priority, int maxDuration)
        {
            Task task = new Task(() => func());
            MetadataForTask md = new MetadataForTask(token, priority, maxDuration, task);

            database.Add(task, md);
            pendingTasks.Add(priority, task);

            SchedulePendingTasks();
        }


        // interno koristena funkcija koja pri svakom pozivu potencijalno pokrece novo rasporedjivanje
        // u zavisnosti od moda rada koji je korisinik odabrao, tj. da li ce rasporedjivanje biti preventivno ili nepreventivno
        internal void SchedulePendingTasks()
        {
            lock (locker)
             {
                AbortTasksOverQuota();

                if (_mode == Mode.NonPreemptive)
                    ScheduleTasksOnAvailableThreadsNonPreemptive();
                else
                    ScheduleTasksOnAvailableThreadsPreemptive();
              }
        }

        // funkcija koja provjerava ima li zadataka koji su zavrseni ili otkazani, a da se trenutno izvrsavaju 
        // u slucaju da postoje, oni se uklanjaju iz tog niza, kao i iz kolekcije svih zadataka
        internal void AbortTasksOverQuota()
        {          
            for (int i = 0; i < _maxDegreeOfParallelism; i++)
            {
                if (executingTasks[i]!=null)
                {
                   Task executingTask = executingTasks[i];
                    if (executingTask.IsCanceled || executingTask.IsCompleted)
                    {
                        executingTasks[i] = null;                
                        database[executingTask].Token.Cancel();
                        pendingTasks.RemoveAt(pendingTasks.IndexOfValue(executingTask));
                    }
                }
            }           
        }

        // funkcija koja iz kolekcije svih zadataka uzima one koji se trenutno ne izvrsavaju i pokrece ih na slobodnim tokovima izrsavanja
        // nema preuzimanja vremena, kada se zadatak pokrene izvrsava se dok se ne zavrsi ili mu istekne maksimalno vrijeme izvrsavanja
        internal void ScheduleTasksOnAvailableThreadsNonPreemptive()
        {
            int[] availableThreads = executingTasks.Select((value, i) => (value, i)).Where(x => x.value==null).Select(x => x.i).ToArray();
            foreach (int freeThread in availableThreads)
            {
                Task task = pendingTasks.Values.FirstOrDefault((t) => !database[t].Working && !t.IsCompleted);
                // Task task = pendingTasks.First().Value;
              
                if (task != null)
                {
                  //  Console.WriteLine($"{task.Id} {Thread.CurrentThread.ManagedThreadId}
                    database[task].Working = true;         
                    task.Start();
                    Task taskWithCallback = Task.Factory.StartNew(() =>
                    {
                        Task.Delay(database[task].MaxDuration*1000).Wait();
                        database[task].Token.Cancel();
                        task.Wait();
                        SchedulePendingTasks();
                    });
                    database[task].TaskWithCallback = taskWithCallback;
                    executingTasks[freeThread] = task;
                }
            }
        }

        // funkcija koja vrsi preventivno rasporedjivanje, tako sto svaki put provjeri da li postoje slobodne niti za izvrsavanje
        // ukoliko postoje rasporedi neke zadatke na njih
        // svaki put provjerava i da li medju zadacima koji se trenutno ne izvrsavaju ima neki zadatak sa vecim prioritetom od nekog zadataka koji se izvrsava
        // i koji bi mogao da ga zamijeni
        internal void ScheduleTasksOnAvailableThreadsPreemptive()
        {
            int[] availableThreads = executingTasks.Select((value, i) => (value, i)).Where(x => x.value==null).Select(x => x.i).ToArray();
            if(availableThreads.Length > 0)
            {
                foreach (int freeThread in availableThreads)
                {
                    Task task = pendingTasks.Values.FirstOrDefault((t) => !database[t].Working && !t.IsCompleted);                  
                    if (task != null)
                    { 
                         database[task].Working = true;
                        if (database[task].Token.IsOnWait)
                        {
                            database[task].Token.ResetWait();
                            database[task].Token.Handler.Set();
                            
                            Task taskWithCallback = Task.Factory.StartNew(() =>
                            {
                                long timeLeft = database[task].MaxDuration * 1000 - database[task].stopwatch.ElapsedMilliseconds;
                                Task.Delay((int)timeLeft).Wait();   //potencijalno problem
                                if (!database[task].Token.IsOnWait)  //ako nije u medjuvremenu opet izbacen task
                                {
                              //      Console.WriteLine("Moj zadatak " + task.Id + " nije na cekanju i ja cu sad da ga otkazem");
                                    database[task].Token.Cancel();
                                    task.Wait();
                                    SchedulePendingTasks();  //mozda je ovo problem
                                }                               
                            });
                            database[task].TaskWithCallback = taskWithCallback;
                            database[task].stopwatch.Restart();  //restart stopericu
                        }
                        else
                        {                        
                            task.Start();
                            database[task].stopwatch.Start();
                            Task taskWithCallback = Task.Factory.StartNew(() =>
                            {
                                Task.Delay(database[task].MaxDuration * 1000).Wait();
                                if (!database[task].Token.IsOnWait)
                                {                       
                                    database[task].Token.Cancel();
                                    task.Wait();
                                    SchedulePendingTasks();
                                }
                              
                            });
                            database[task].TaskWithCallback = taskWithCallback;
                        }
                        executingTasks[freeThread] = task;
                    }
                }
            }
         
               
            var potentialExecution = pendingTasks.Where(x => !database[x.Value].Working).ToList();
            var lista = executingTasks.Select((value, i) => (value, i)).Where(x => x.value!=null).OrderByDescending(x => database[x.value].Priority).ToList();
                
            foreach (var pair in potentialExecution)
            {                   
                if(lista.Count>0 && database[pair.Value].Priority< database[lista[0].value].Priority)
                {
                    Task t = lista[0].value;
                    int index = lista[0].i;
                    database[t].Token.SetWait();
                    database[t].stopwatch.Stop();
                    database[t].Working = false;
                       
                    Task forWork = pair.Value;
                    database[forWork].Working = true;

                    if (database[forWork].Token.IsOnWait)  //pokrecem ga opet
                    {
                        database[forWork].Token.ResetWait();
                        database[forWork].Token.Handler.Set();

                        Task taskWithCallback = Task.Factory.StartNew(() =>
                        {
                            long timeLeft = database[forWork].MaxDuration * 1000 - database[forWork].stopwatch.ElapsedMilliseconds;
                            Task.Delay((int)timeLeft).Wait();  
                            if (!database[forWork].Token.IsOnWait)  
                            {
                            //      Console.WriteLine("Moj zadatak " + forWork.Id + " nije na cekanju i ja cu sad da ga otkazem");
                                database[forWork].Token.Cancel();
                                forWork.Wait();
                                SchedulePendingTasks();  
                            }

                        });
                        database[forWork].TaskWithCallback = taskWithCallback;
                        database[forWork].stopwatch.Restart(); 
                    }
                    else
                    {
                    //       Console.WriteLine($"{forWork.Id} {Thread.CurrentThread.ManagedThreadId}");
                        forWork.Start();
                        database[forWork].stopwatch.Start();
                        Task taskWithCallback = Task.Factory.StartNew(() =>
                        {
                            Task.Delay(database[forWork].MaxDuration * 1000).Wait();
                            if (!database[forWork].Token.IsOnWait)
                            {                         
                                database[forWork].Token.Cancel();
                                forWork.Wait();
                                SchedulePendingTasks();

                            };
                        });
                        database[forWork].TaskWithCallback = taskWithCallback;
                    }
                    executingTasks[index] = forWork;
                    lista.RemoveAt(0);
                }              
            }
        }
       
        /// <summary>
        /// Konstruktor klase MyScheduler
        /// </summary>
        /// <param name="maxDegreeOfParallelism"> broj niti kojima rasporedjivac raspolaze</param>
        /// <param name="mode"> mod rada, mogucnosti: preventivno i nepreventivno</param>
        public MyScheduler(int maxDegreeOfParallelism, Mode mode)
        {
            if (maxDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException("maxDegreeOfParallelism");
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
            executingTasks = new Task[_maxDegreeOfParallelism];
            _mode = mode;
            Resource.InitializeResources(); //inicijalizuje resurse
            Resource.SetScheduler(this);
        }


        // metode i polja koja koristi ugradjeni rasporedjivac

        // kolekcija koja predstavlja PriorityQueue, zadaci su sortirani po prioritetu, oni sa istim prioritetom su u FIFO poretku
        private readonly SortedList<int, Task> tasks = new SortedList<int, Task>(new MyKeyComparer<int>());
   
        // broj niti koji se trenutno izvrsava
        private int delegatesQueuedOrRunning = 0;

        public override int MaximumConcurrencyLevel { get { return _maxDegreeOfParallelism; } }


        /// <summary>
        /// Metoda kojom se prosljedjuju zadaci koji ce da se rasporedjuju na ugradjenom rasporedjivacu.
        /// </summary>
        /// <param name="task"> Zadatak koji se rasporedjuje.</param>
        /// <param name="token"> Token koji ta funkcija koristi za kooperativno zaustavljanje.</param>
        /// <param name="priority"> Prioritet funkcije pri izvrsavanju. (manja vrijednost znaci veci prioritet)</param>
        /// <param name="maxDuration"> Maksimalno predvidjeno vrijeme trajanja izvrsavanja.</param>
        public void RegisterTask(Task task, MyToken token, int priority, int maxDuration)
        {
            MetadataForTask md = new MetadataForTask(token, priority, maxDuration, task);

            database.Add(task, md);
        }

        protected override void QueueTask(Task task) 
        {
            lock (tasks)
            {
                tasks.Add(database[task].Priority, task);
                if (delegatesQueuedOrRunning < _maxDegreeOfParallelism)
                {
                    ++delegatesQueuedOrRunning;
                    NotifyThreadPool();
                }
            }
        }

        // metoda koja se poziva ukoliko je pri dodavanju novog zadatka metodom QueueTask 
        // broj niti koje se trenutno izvrsvaju manji od maksimalnog broja niti koje se mogu izvrsavati paralelno
        private void NotifyThreadPool()
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {              
                    while (true)
                    {
                        Task item;
                        lock (tasks)
                        {
                            if (tasks.Count == 0)
                            {
                                --delegatesQueuedOrRunning;
                                break;
                            }
                            item = tasks.First().Value;
                            tasks.RemoveAt(0);
                        }

                        Task taskWithCallback = Task.Factory.StartNew(() =>
                        {
                            Task.Delay(database[item].MaxDuration * 1000).Wait();
                            database[item].Token.Cancel();
                        });
                        base.TryExecuteTask(item);
                    }
            }, null);
        }


        protected override IEnumerable<Task> GetScheduledTasks() 
        {
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(tasks, ref lockTaken);
                if (lockTaken) return tasks.Values;
                else throw new NotSupportedException();
            }
            finally
            {
                if (lockTaken) Monitor.Exit(tasks);
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)  //pokusava da ga izvrsi
        {
            return false;
        }

        protected override bool TryDequeue(Task task)
        {
            lock (tasks)
            {
                if (tasks.ContainsValue(task))
                {
                    tasks.RemoveAt(tasks.IndexOfValue(task));
                    return true;
                }
                else return false;
            }
        }


        
    }
    /// <summary>
    /// Klasa koja predstavlja dijeljene resurse.
    /// </summary>
    public class Resource : Object
    {
        // ime resursa
        private string name;

        // rasporedjivac na kome se rasporedjuju zadaci koji koriste dijeljene resurse
        private static MyScheduler myScheduler;

        // objekat za sinhronizaciju
        readonly static Object locker = new object();
        internal readonly static Object lockForOwners = new object();

        // objekat klase graf koji omogucava detekciju deadlocka
        readonly static Graph myGraph = new Graph();

        // objekat na kome se zadaci blokiraju cekajuci da dobiju resurs
        private EventWaitHandle handler = new EventWaitHandle(false, EventResetMode.AutoReset);

        // postavlja rasporedjivac za date resurse
        internal static void SetScheduler(MyScheduler scheduler)
        {
            myScheduler = scheduler;
        }

        // kolekcija koja povezuje resurs sa zadatkom koji trenutno posjeduje taj resurs
        private static Dictionary<Resource, Task> resourceOwners = new Dictionary<Resource, Task>();

        // metoda koja vraca objekat klase Task koji koristi zadati resurs
        internal static Task GetOwner(Resource resource)
        {
            lock (lockForOwners)
            {
                return resourceOwners[resource];
            }

        }

        // metoda koja vraca resurs po imenu koje je proslijedjeno kao argument metode
        internal static Resource GetResourceByName(string name)
        {
            lock (lockForOwners)
            {
                return resourceOwners.Keys.First(r => r.name.Equals(name));
            }
        }

        // metoda koja resursu r, dodjeljuje vlasnika task
        internal static void SetOwner(Resource r, Task task)
        {
            lock (lockForOwners)
            {
                resourceOwners[r] = task;
            }

        }

        // konstruktor objekta klase resurs koji je privatan, jer se novi resursi koji su na raspolaganju zadacima koji koriste rasporedjivac
        // dodaju samo iz metode InitializeResources
        private Resource(string name)
        {
            this.name = name;
        }

        // metoda koja postavlja dostupne resurse
        internal static void InitializeResources()
        {
            resourceOwners.Add(new Resource("FAJL1"), null);
            resourceOwners.Add(new Resource("FAJL2"), null);
            resourceOwners.Add(new Resource("FAJL3"), null);
            resourceOwners.Add(new Resource("FAJL4"), null);

        }

        /// <summary>
        ///  Metoda koja omogucava sinhronizovan pristup dijeljenim resursima.
        /// </summary>
        /// <param name="task"> Zadatak koji zahtjeva resurs.</param>
        /// <param name="name"> Ime resursa koji je potreban zadatku.</param>
        /// <returns> vraca resurs, koji funkcija koja predstavlja zadatak sada moze koristiti ili null ukoliko resurs pod proslijedjenim imenom
        /// ne postoji</returns>
        public static Resource LockMeResource(Task task, string name)
        {
            Resource needed = Resource.GetResourceByName(name);
            lock (locker)
            {
                if (needed == null) return null;

                Task owner = GetOwner(needed);
                if (owner == null)  //niko ne drzi taj resurs
                {
                    //   resourceOwners[needed] = task;
                    SetOwner(needed, task);
                    myScheduler.database[task].SetMaximumPriority();
                    myGraph.AddEdge(needed, task);
                    return needed;
                }
                else
                {
                    // Console.WriteLine("nije slobodan resurs "+ needed.name);
                    myGraph.AddEdge(task, needed);
                    HashSet<Object> visited = new HashSet<object>();
                    HashSet<Object> recursionStack = new HashSet<object>();

                    if (Graph.CheckDeadlock(myGraph, task, ref visited, ref recursionStack))  //provjeri da li je doslo do deadlocka
                    {
                        Console.WriteLine("DEADLOCK");
                        myGraph.DeadlockSolver(task);
                        myScheduler.database[task].Token.Cancel();
                        return null;
                    }
                }
            }
            needed.handler.WaitOne();

            lock (locker)
            {
                myGraph.RemoveEdge(task, needed);
                myScheduler.database[task].SetMaximumPriority();
                myGraph.AddEdge(needed, task);
                SetOwner(needed, task);
                myScheduler.SchedulePendingTasks();
                return needed;
            }
           

            /*     if (myScheduler.database[task].Token.IsOnWait)
                 {
                     myScheduler.database[task].Token.Handler.WaitOne();
                 }*/
        }

        /// <summary>
        /// Metoda koja omogucava oslobadjanje prethodno dobijenog resursa, uz sinhronizovan pristup.
        /// </summary>
        /// <param name="task"> Zadatak koji oslobadja resurs.</param>
        /// <param name="resource"> Resurs koji ce biti oslobodjen, koji se vise ne koristi u korisnickoj funkciji.</param>
        public static void UnlockResource(Task task, Resource resource)
        {
            lock (locker)
            {
                myGraph.RemoveEdge(resource, task);
                SetOwner(resource, null);
                //  resourceOwners[resource] = null;
                myScheduler.database[task].ReturnPreviousPriority();
                resource.handler.Set();
            }
        }
    }
}

# MyScheduler

MyScheduler je klasa koja predstavlja raspoređivač zadataka koji radi sa prioritetima i omogućava specifikaciju broja niti na kojima se zadaci mogu raspoređivati. Raspoređivač je implementiran tako da nasljeđuje klasu TaskScheduler, te time omogućava dva pritupa raspoređivanju. Jedan način je korištenjem API-ja obezbjeđenog od strane frameworka, a drugi način je upotrebom slobodno definisanog API-ja.

Bilo da korisnik želi da zadatke raspoređuje na jedan ili drugi način, oboje je moguće izvršiti sa jednom instancom raspoređivača, dok se metode koje je potrebno pozivati razlikuju.

Slobodno definisana implementacija raspoređivača omogućava korisniku i preventivno i nepreventivno raspoređivanje, a u slučaju ugrađenog raspoređivača zadatke je moguće raspoređivati isključivo nepreventivno.

Da bi se kreirao objekat klase MyScheduler, potrebno je pozvati konstruktor:
### **MyScheduler(int maxDegreeOfParallelism, Mode mode)**

Konstruktor prima dva parametra, od kojih prvi predstavlja specifikaciju broja niti sa kojima raspoređivač raspolaže, a drugim parametrom se definiše da li će raspoređivanje biti preventivno ili nepreventivno. Konstruktor se nalazi u biblioteci MyLibrary, tako da je korisnicima pristup moguć sa imenom MyLibrary.MyScheduler.

##### *Napomena:* U slučaju korištenja raspoređivača obezbjeđenog od strane frameworka, bilo da je objekat MyScheduler kreiran sa argumentom MyLibrary.MyScheduler.Mode.Preemptive ili MyLibrary.MyScheduler.Mode.NonPreemptive, uvijek će se izvršavati NonPreemptive način rada.
   
## Raspoređivanje zadataka korištenjem slobodno definisanog API-ja

Prilikom korištenja slobodno definisanog API-ja, važno je da li je naveden preventivni ili nepreventivni način funkcionisanja, zbog toga kako će raspoređivač pristupati raspoređivanju.

Nezavisno od toga koji način rada je odabran, korisnik na isti način prosljeđuje željene funkcije tj. zadatke, na izvršavanje.

Prije nego korisnik navede tijelo funkcije koju želi da izvrši potrebno je da od raspoređivača zatraži token, tj. objekat klase MyToken. Puni naziv tipa objekta koji će korisnik dobiti je MyLibrary.MyScheduler.MyToken.

Objekti klase MyToken obezbjeđuju saradnju, odnosno kooperativnost između raspoređivača i korisnika njegovih usluga, te omogućavaju njegovo ispravno funkcionisanje.

Da bi korisnik dobio token koji će koristiti u svojoj funkciji, i time kako je već navedeno, obezbijediti kooperativnost između onoga ko piše zadatak, i onoga ko ga raspoređuje, poziva metodu:
 
### **MyToken CreateMyToken()**

preko objekta klase MyScheduler. 

Nakon što se dobije token, slijedi definicija funkcije.

**Važno:** Funkcija koja se definiše mora biti povratnog tipa void i mora imati praznu listu parametara.

U svakoj funkciji koja se izvršava potrebno je koristiti token na ispravan način, bez toga se ne garantuje da će zadaci biti raspoređivani ispravno i da neće doći do grešaka.

U tijelu funkcije je potrebno provjeravati dvije stavke, naročito u slučaju da funkcija sadrži određenu petlju, potrebno je u svakoj iteraciji provjeriti da li je token otkazan i da li je postavljen na čekanje.

Provjera da li je token otkazan zapravo predstavlja provjeru da li je zadatak otkazan, tj. da li je isteklo maksimalno predviđeno vrijeme za njegovo izvršavanje.

To se radi tako što se provjerava property tokena:
### **token.IsCanceled**

U slučaju da navedena konstrukcija, u kojoj je token objekat klase MyToken, vrati true, potrebno je obezbijediti prekid izvršavanja funkcije, najčešće naredbom return ili break.

Provjera da li je token postavljen na čekanje, obezbjeđuje  pauziranje zadatka. Provjerava se sa:

### **token.IsOnWait**

U slučaju da konstrukcija vrati true, znači da je izvršavanje prekinuto jer je neki drugi zadatak većeg prioriteta stigao na izvršavanje, te zadatak koji je predstavljen funkcijom u kojoj se ovaj token koristi mu prepušta nit izvršavanja. Kada je povratna vrijednost true, potrebno je izvršiti naredbu:

### **token.Handler.WaitOne();**

koja blokira zadatak sve dok interni mehanizam ne odluči da se zadatak nastavi izvršavati.

##### *Napomena:* Maksimalno vrijeme izvršavanja zadatka ne protiče dok je zadatak zaustavljen.

Kada su ispunjeni minimalni zahtjevi za ispravno funkcionisanje raspoređivača (u slučaju korištenja dijeljenih resursa postoje dodatni zahtjevi), zadatak se prosljeđuje na izvršavanje.

##### *Napomena:* Koristi se izraz zadatak, iako naredna funkcija ne prima parametar Task, ali svi njeni argumenti čine logičku cjelinu koja predstavlja zadatak.

### **void ScheduleTask(FunctionForExecution func, MyToken token, int priority, int maxDuration)**

Funkcija ScheduleTask se poziva nakon definicije funkcije za izvršavanje. Kao argumenti joj se prosljeđuju sama funkcija, token, prioritet zadatka, i njegovo maksimalno predviđeno vrijeme izvršavanja.

Primjer načina korištenja:


     MyLibrary.MyScheduler.MyToken tokenForMyFunction = myScheduler.CreateMyToken();
            void print()
            {
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

Da bi se omogućilo da svi zadaci završe, prije nego se završi sam program, potrebno je nakon prosljeđivanja svih zadataka raspoređivaču koristiti sljedeći kod:
  
    while (myScheduler.CurrentTaskCount > 0)
            {
  
                Task.Delay(oneSecondDelayInMilliseconds).Wait();
            }

Vrijeme navedeno kao argument metode Delay, može biti promijenjeno, ali je mehanizam važan da se program ne bi završio ili prešao na dio koda koji se ne odnosi na raspoređivanje. CurrentTaskCount vraća broj zadataka koji još nisu završili za izvršavanjem.

## Raspoređivanje zadataka korištenjem ugrađenog raspoređivača

Ovo raspoređivanje koristi redefinisane metode klase TaskScheduler.

Ukoliko korisnik želi da koristi ovaj mehanizam, potrebno je da obezbijedi određenu kolekciju u koju će ubacivati zadatke koje bude kreirao (npr. List).

Prije kreiranja instanci klase Task koje se koriste u ovom slučaju, potrebno je za svaki zadatak zatražiti token od raspoređivača na isti način kao što je to opisano kod rasporedjivanja upotrebom slobodno definisanog API-ja.

Zatim se kreira objekat klase Task, korištenjem konstruktora
### **Task(Action)**

pri čemu se kao action prosljeđuje lambda izraz koji predstavlja tijelo funkcije koja će se izvršavati. U toj funkciji je potrebno koristiti prethodno dobijeni token na isti način kao što je već opisano, te time obezbijediti kooperativnost. Funkcija koja se piše mora biti bez parametara, i povratnog tipa void.

Nakon kreiranja objekta klase Task sa navedenim konstruktorom, potrebno je pozvati metodu iz klase MyScheduler:

### **void RegisterTask(Task task, MyToken token, int priority, int maxDuration)**

Ovoj metodi se prosljeđuje kreirani zadatak, zajedno sa tokenom, prioritetom i maksimalnim predviđenim vremenom izvršavanja za taj zadatak.

Nakon toga je potrebno dodati objekat klase Task u kolekciju koju je korisnik odabrao, te pokrenuti sam zadatak.\
npr. dodavanje u listu:

#### **void Add(Task)**

Zadatak se pokreće isključivo korištenjem metode:

### **void Start(TaskScheduler)** 
kojoj se prosljeđuje instanca klase MyScheduler.

Primjer načina korištenja:

      MyLibrary.MyScheduler.MyToken tk3 = myScheduler.CreateMyToken();
              Task t3 = new Task(() =>
              {
                  for (int i = 0; i < 5; i++)
                  {
                      if (tk3.IsCanceled)
                      {
                          break;
                      }
                    
                    Console.WriteLine($"Ja sam zadatak 3 i izvrsavam se {i} ");
                    Task.Delay(oneSecondDelayInMilliseconds).Wait();
                  }
              });

              myScheduler.RegisterTask(t3,tk3, 3,5);
              tasks.Add(t3);
              t3.Start(myScheduler);

Nakon pokretanja svih zadataka potrebno je sačekati da svi oni završe sa izvršavanjem, npr. naredbom:

### **Task.WaitAll(list.ToArray());**

gdje je list lista objekata tipa Task, u koju smo ih sve i dodali, ili neka proizvoljna kolekcija.

## Pristup dijeljenim resursima

Klasa MyScheduler omogućava korisnicima mehanizam pristupa dijeljenim resursima. Dijeljeni resursi su definisani u samoj biblioteci.
 
Kada korisnik želi da zatraži resurs, to čini pozivanjem metode:

### **public static Resource LockMeResource(Task task, string name)**

Parametri metode su objekat klase Task koji predstavlja zadatak koji izvršava datu funkciju, a name je ime resursa.
Ukoliko korisnik zatraži resurs koji ne postoji funkcija vraća null, inače vraća objekat klase Resource.
Puni naziv za pristup objektu je MyLibrary.Resource. 

Prvi argument koji se prosljeđuje je Task koji izvršava datu funkciju, kojem korisnik može pristupiti jedino preko propertija MyTask objekta klase MyToken, odnosno funkciji prosljeđuje token.MyTask.

Nakon što dobije resurs korisnik mora provjeriti da li mu je otkazan token na prethodno definisan način, jer u slučaju da traženjem resursa dođe do deadlock-a, elementarno razrješavanje je prekidanje zadatka koji je izazvao deadlock, te je to potrebno i obezbijediti u funkciji.

##### *Napomena:* Dostupne resurse za raspoređivač je moguće dodati samo iz internog mehanizma, tj. u saradnji sa programerom.

Kada je korisnik završio sa korištenjem resursa, potrebno je pozvati metodu:
### **public static void UnlockResource(Task task, Resource resource)**

koja prima zadatak koji otpušta resurs, i resurs koji će biti otpušten. Zadatak koji prima resurs je isti zadatak koji je prosljeđen kao argument metode LockMeResource, i pristupa mu se na isti način, preko objekta tokena, tj. sa token.MyTask.

Primjer korištenja:

     MyLibrary.Resource res1 = MyLibrary.Resource.LockMeResource(tk1.MyTask, "FAJL2");
                      if (tk1.IsCanceled) return;
                      Console.WriteLine($"Ja sam zadatak 1 i dobio sam FAJL2 ");

                      Task.Delay(oneSecondDelayInMilliseconds).Wait();
                      MyLibrary.Resource.UnlockResource(tk1.MyTask, res1);

 *Napomena:* U radu sa resursima koristi se Non Preemption Protocol (NPP) koji predstavlja jedan on načina sprječavanja pojave inverzije prioriteta. To znači da svakom zadatku koji zatraži resurs, i uspješno ga dobije, biva dodijeljen maksimalni prioritet, odnosno za vrijeme korištenja resursa ne dolazi do preventivnog raspoređivanja na niti na kojoj se taj zadatak izvršava izvršava.

## Razrješavanje Deadlock situacija

Kao što je već navedeno, u slučaju pojave deadlock-a, raspoređivač će da otkaže zadatak koji ga je prouzrokovao. Potrebno je da korisnik nakon traženja resursa od raspoređivača provjeri da li je upravo on taj koji je prouzrokovao deadlock, tj. da li mu je token otkazan. Provjeru da li je token otkazan korisnik vrši na isti način na koji je ranije navedeno, tj. provjerom token.IsCanceled.


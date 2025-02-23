using System.Diagnostics;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PerformanceDemo
{
	class Program
	{
		static async Task Main(string[] args)
		{
			//Console.WriteLine("C# Performans Teknikleri Demonstrasyonu");
			//Console.WriteLine("---------------------------------------");

			//Console.WriteLine("1. Span<T> ve Memory<T> Kullanımı");
			//SpanVsArrayDemo();

			//Console.WriteLine("\n2. ReaderWriterLockSlim Kullanımı");
			//await ReaderWriterLockDemo();

			//Console.WriteLine("\n3. AsyncLocal<T> ile Asenkron Bağlam Aktarımı");
			//await AsyncLocalDemo();

			//Console.WriteLine("\n4. LINQ'de ToHashSet() ve ToDictionary() ile Optimizasyon");
			//LINQOptimizationDemo();


			Console.WriteLine("\n5. Channel<T> Kullanımı");
			await ChannelUsage();


			Console.WriteLine("\nDemo tamamlandı. Çıkmak için bir tuşa basın.");
			Console.ReadKey();



		}

		#region 1. Span<T> ve Memory<T> Demonstrasyonu
		static void SpanVsArrayDemo()
		{
			// 1 milyon baytlık veri oluşturuluyor
			byte[] data = new byte[1_000_000];
			new Random().NextBytes(data);

			int iterations = 10000;
			Stopwatch sw = Stopwatch.StartNew();
			long dummySum = 0;

			// Span<T> kullanarak dilimleme: ekstra kopya olmadan çalışır
			for (int i = 0; i < iterations; i++)
			{
				ReadOnlySpan<byte> span = data;
				var header = span.Slice(0, 10);   // sadece referans dilim
				var payload = span.Slice(10);
				// Basit toplama işlemi (işlem süresine etki etmesin diye)
				foreach (var b in header) dummySum += b;
				foreach (var b in payload) dummySum += b;
			}
			sw.Stop();
			Console.WriteLine($"Span<T> ile dilimleme süresi: {sw.ElapsedMilliseconds} ms");
			//Span<T> ile dilimleme süresi: 20299 ms

			// Geleneksel dizilerde Array.Copy kullanılarak yapılan dilimleme (ek kopyalama var)
			sw.Restart();
			dummySum = 0;
			for (int i = 0; i < iterations; i++)
			{
				byte[] header = new byte[10];     // Heap tahsisi
				byte[] payload = new byte[data.Length - 10]; // Heap tahsisi
				Array.Copy(data, 0, header, 0, 10);
				Array.Copy(data, 10, payload, 0, data.Length - 10);
				foreach (var b in header) dummySum += b;
				foreach (var b in payload) dummySum += b;
			}
			sw.Stop();
			Console.WriteLine($"Array.Copy ile dilimleme süresi: {sw.ElapsedMilliseconds} ms");
			//Array.Copy ile dilimleme süresi: 10183 ms
		}
		#endregion


		#region 2. ReaderWriterLockSlim Demonstrasyonu
		static async Task ReaderWriterLockDemo()
		{
			// Paylaşılan sözlük ve kilit oluşturuluyor
			Dictionary<string, string> config = new Dictionary<string, string>();
			ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();

			// Yazma işlemi: 1000 kez yeni veri ekleniyor
			Task writeTask = Task.Run(() =>
			{
				for (int i = 0; i < 1000; i++)
				{
					rwLock.EnterWriteLock();
					try
					{
						config["key" + i] = "value" + i;
					}
					finally
					{
						rwLock.ExitWriteLock();
					}
					Thread.Sleep(1); // Gerçek senaryoyu taklit etmek için küçük gecikme
				}
			});



			// Okuma işlemi: paralel olarak veriyi okumaya çalışıyoruz
			Task readTask = Task.Run(() =>
			{
				int localSum = 0;
				for (int i = 0; i < 1000; i++)
				{
					rwLock.EnterReadLock();
					try
					{
						if (config.TryGetValue("key" + i, out string value))
						{
							localSum += value.Length;
						}
					}
					finally
					{
						rwLock.ExitReadLock();
					}
					Thread.Sleep(1);
				}
				Console.WriteLine($"Okuma işlemi sonucu: {localSum}"); //Okuma işlemi sonucu: 3912




			});


			await Task.WhenAll(writeTask, readTask);
			Console.WriteLine("ReaderWriterLockSlim demo tamamlandı.");
		}


		/*
		     OLMASI GEREKEN;

				     10 tane “valueX” (X: 0-9) → 6 * 10 = 60
                     90 tane “valueXX” (X: 10-99) → 7 * 90 = 630
                     900 tane “valueXXX” (X: 100-999) → 8 * 900 = 7200
                     --------------------------------------
                     Toplam: 7890 karakter (ideal durumda)
				
				3912 karakter olduğu için yaklaşık % 50’sinde başarılı okuma yapılabilmiş gibi görünüyor.

				Veri hala bellekte var, ancak okuma işlemi yazma işlemiyle tam senkronize çalışmadığı için bazı döngülerde çağrı başarısız oluyor.
				 
				 Eğer ki, await writeTask ve await readTask diyerek aynı anda değilde farklı farklı işlemler yapılsaydı 7890 karakter elde ederdik.
	   */


		#endregion



		#region 3. AsyncLocal<T> Demonstrasyonu
		// AsyncLocal ile her isteğe özgü bağlamın taşınması sağlanıyor
		private static AsyncLocal<string> _correlationId = new AsyncLocal<string>();

		static async Task AsyncLocalDemo()
		{
			// Aynı anda iki farklı istek simüle ediliyor
			Task task1 = ProcessRequestAsync("REQUEST-1");
			Task task2 = ProcessRequestAsync("REQUEST-2");
			await Task.WhenAll(task1, task2);
		}


		static async Task ProcessRequestAsync(string requestId)
		{
			_correlationId.Value = requestId;
			Console.WriteLine($"[{requestId}] Başlangıçta CorrelationId: {_correlationId.Value}");
			await Task.Delay(500); // Asenkron işlem simülasyonu
			Console.WriteLine($"[{requestId}] Await sonrası CorrelationId: {_correlationId.Value}");
		}
		#endregion




		#region 4. LINQ'de ToHashSet() ve ToDictionary() Optimizasyonu
		static void LINQOptimizationDemo()
		{
			int size = 10000;
			var numbers = Enumerable.Range(0, size).ToList();
			var lookupNumbers = Enumerable.Range(size / 2, size / 2).ToList();


			//List.Contains() Kullanımı (Zayıf Performans)
			//List.Contains() O(n) zaman karmaşıklığına sahiptir.
			//Her bir eleman için Contains() çağrıldığında, listenin tamamı taranır.
			//Büyük veri setlerinde çok yavaştır.
			//Sonuç: List araması yavaş!

			Stopwatch sw = Stopwatch.StartNew();
			int count = 0;
			foreach (var num in lookupNumbers)
			{
				if (numbers.Contains(num))
				{
					count++;
				}
			}
			sw.Stop();
			Console.WriteLine($"List.Contains() ile arama süresi: {sw.ElapsedMilliseconds} ms, bulunan eleman: {count}");




			// HashSet kullanarak arama işlemi (O(1))
			var hashSet = numbers.ToHashSet();
			sw.Restart();
			count = 0;
			foreach (var num in lookupNumbers)
			{
				if (hashSet.Contains(num))
				{
					count++;
				}
			}
			sw.Stop();
			Console.WriteLine($"HashSet.Contains() ile arama süresi: {sw.ElapsedMilliseconds} ms, bulunan eleman: {count}");


			/*
			  ToHashSet() ile liste HashSet’e dönüştürülüyor.
              HashSet, elemanları hash’leyerek sakladığı için aramalar O(1) sürede gerçekleşir.
              Büyük veri setlerinde çok daha hızlıdır!
              Sonuç: HashSet, List’e göre çok daha hızlı!
			 */

			// ToDictionary() örneği: ürün listesi üzerinden hızlı arama
			var products = Enumerable.Range(0, 10000)
									 .Select(i => new Product { Id = i, Name = "Ürün" + i })
									 .ToList();
			sw.Restart();
			var productDict = products.ToDictionary(p => p.Id);
			sw.Stop();
			Console.WriteLine($"ToDictionary() oluşturma süresi: {sw.ElapsedMilliseconds} ms");


			/*
			   List, Dictionary’ye çevriliyor.
               Dictionary’de arama O(1) sürede çalışır (HashMap yapısı sayesinde).
               Sonuç: Dictionary oluşturma süresi var, ancak lookup süresi çok hızlı! 
			 */


			sw.Restart();
			if (productDict.TryGetValue(5000, out Product foundProduct))
			{
				// Basit lookup işlemi
				string productName = foundProduct.Name;
			}
			sw.Stop();
			Console.WriteLine($"Dictionary lookup süresi: {sw.ElapsedTicks} ticks");
		}
		#endregion

		#region Channel<T> Kullanımı


		static async Task ChannelUsage()
		{
			var channel = Channel.CreateUnbounded<int>();


			_ = Task.Run(async () =>
			{
				for (int i = 1; i <= 10; i++)
				{
					await channel.Writer.WriteAsync(i); // Sensör verisini yaz
					Console.WriteLine($"Üretildi: {i}");
					await Task.Delay(200); // Sensör verisi 200ms'de bir geliyor
				}
				channel.Writer.Complete(); // Yazma işlemi tamamlandı
			});

			// Tüketici (Consumer): Verileri işleyen servis
			await Task.Run(async () =>
			{
				await foreach (var data in channel.Reader.ReadAllAsync())
				{
					Console.WriteLine($"Tüketildi: {data}");
					await Task.Delay(500); // İşlem süresi 500ms
				}
			});


		}

		/*
		   Thread-safe çalışır, ek lock mekanizmalarına gerek yoktur.
           Asenkron çalıştığı için ana thread’i bloke etmez.
           Producer ve Consumer birbirinden bağımsız hızlarda çalışabilir.
           Özellikle mikro servisler veya IoT sistemleri için idealdir.
		*/



		#endregion


	}

	class Product
	{
		public int Id { get; set; }
		public string Name { get; set; }
	}
}

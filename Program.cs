using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;

class Program
{
    const string DbFile = "primes.db";
    const string ConnStr = "Data Source=" + DbFile;
    const int SegmentSize = 1_000_000;
    const int BatchSize = 500;

    static void Main()
    {
        EnsureDatabase();
        long start = GetLastPrime() + 1;

        Console.WriteLine($"Iniciando busca a partir de {start}...");

        while (true)
        {
            long end = start + SegmentSize;
            Console.WriteLine($"Verificando intervalo: {start} a {end}");

            var primes = SegmentedSieve(start, end);
            Console.WriteLine($"Encontrados {primes.Count} primos. Salvando...");

            SavePrimes(primes);
            start = end;
        }
    }

    static void EnsureDatabase()
    {
        bool createTable = !File.Exists(DbFile);

        if (createTable)
            SQLiteConnection.CreateFile(DbFile);

        using var conn = new SQLiteConnection(ConnStr);
        conn.Open();

        // Verifica se a tabela existe
        using (var checkCmd = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='primes';", conn))
        {
            var exists = checkCmd.ExecuteScalar();
            if (exists == null)
            {
                using var cmd = new SQLiteCommand("CREATE TABLE primes (value TEXT PRIMARY KEY);", conn);
                cmd.ExecuteNonQuery();
                cmd.CommandText = "INSERT INTO primes (value) VALUES (2);";
                cmd.ExecuteNonQuery();
            }
        }
    }

    static long GetLastPrime()
    {
        using var conn = new SQLiteConnection(ConnStr);
        conn.Open();
        using var cmd = new SQLiteCommand("SELECT value FROM primes ORDER BY value DESC LIMIT 1;", conn);
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt64(result) : 2;
    }

    static List<long> SegmentedSieve(long start, long end)
    {
        var limit = (int)Math.Sqrt(end) + 1;
        var smallPrimes = SimpleSieve(limit);

        var isPrime = new bool[end - start + 1];
        Array.Fill(isPrime, true);

        Parallel.ForEach(smallPrimes, p =>
        {
            long begin = Math.Max(p * p, ((start + p - 1) / p) * p);
            for (long j = begin; j <= end; j += p)
            {
                isPrime[j - start] = false;
            }
        });

        var result = new List<long>();
        for (long i = start; i <= end; i++)
        {
            if (i > 1 && isPrime[i - start])
                result.Add(i);
        }

        return result;
    }

    static List<int> SimpleSieve(int limit)
    {
        var isPrime = new bool[limit + 1];
        Array.Fill(isPrime, true);
        isPrime[0] = isPrime[1] = false;

        for (int i = 2; i * i <= limit; i++)
        {
            if (!isPrime[i]) continue;
            for (int j = i * i; j <= limit; j += i)
                isPrime[j] = false;
        }

        var primes = new List<int>();
        for (int i = 2; i <= limit; i++)
        {
            if (isPrime[i]) primes.Add(i);
        }

        return primes;
    }

    static void SavePrimes(List<long> primes)
    {
        using var conn = new SQLiteConnection(ConnStr);
        conn.Open();

        using var tx = conn.BeginTransaction();
        using var cmd = new SQLiteCommand("INSERT OR IGNORE INTO primes (value) VALUES (@val);", conn, tx);
        var param = cmd.CreateParameter();
        param.ParameterName = "@val";
        cmd.Parameters.Add(param);

        foreach (var p in primes)
        {
            param.Value = p;
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }
}
using System.Diagnostics;
using NBitcoin;
using Newtonsoft.Json.Linq;

namespace SimpleBitcoinWallet
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Set the limit of the loop
            int loopLimit = 32;

            // Get the number of available CPU cores
            int numberOfCores = Environment.ProcessorCount;

            // Specify the output file path with a timestamp
            string outputPath = Path.Combine(Directory.GetCurrentDirectory(), $"output_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            using (StreamWriter outputFile = new StreamWriter(outputPath))
            {
                await LogAsync(outputFile, $"Running on {numberOfCores} CPU cores");
                await LogAsync(outputFile, "--------------------------------------------------");

                var tasks = Enumerable.Range(0, loopLimit).Select(async i =>
                {
                    // Generate a new private key
                    Key privateKey = new Key();
                    string privateKeyWif = privateKey.GetWif(Network.Main).ToString();

                    // Get the corresponding public key and Bitcoin address
                    (PubKey publicKey, BitcoinAddress address) = GetPublicKeyAndAddress(privateKey);

                    // Get the balance of the Bitcoin address
                    decimal balance = await GetBalanceAsync(address.ToString());

                    // Log the results
                    string result = $"Private Key: {privateKeyWif} | Public Key: {publicKey} | Bitcoin Address: {address} | Balance: {balance} BTC";
                    await LogAsync(outputFile, result);
                    LogResultToConsole(result, balance);
                });

                await Task.WhenAll(tasks);
            }
        }

        private static (PubKey publicKey, BitcoinAddress address) GetPublicKeyAndAddress(Key privateKey)
        {
            PubKey publicKey = privateKey.PubKey;
            BitcoinAddress address = publicKey.GetAddress(ScriptPubKeyType.Legacy, Network.Main);
            return (publicKey, address);
        }

        private static async void LogResultToConsole(string result, decimal balance)
        {
            if (balance > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                await LogAsync(Console.Out, result);
                Console.ResetColor();
            }
            else
            {
                await LogAsync(Console.Out, result);
            }
        }

        public static async Task LogAsync(TextWriter writer, string message)
        {
            await writer.WriteLineAsync(message);
            await writer.FlushAsync();
        }

        public static async Task<decimal> GetBalanceAsync(string address)
        {
            using (var httpClient = new HttpClient())
            {
                RandomizeRequestHeaders(httpClient);

                string requestUrl = $"https://api.blockcypher.com/v1/btc/main/addrs/{address}/balance";

                // Uncomment to wrap requests into a VPN connection
                // string vpnConfigPath = "path/to/your/openvpn-config.ovpn";
                // string vpnUsername = "your-vpn-username";
                // string vpnPassword = "your-vpn-password";

                HttpResponseMessage response;

                //using (var vpnConnection = await ConnectToVPNAsync(vpnConfigPath, vpnUsername, vpnPassword))
                //{
                try
                {
                    response = await httpClient.GetAsync(requestUrl);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while connecting to BlockCypher API: {ex.Message}");
                    throw;
                }
                //}

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Error while retrieving balance from BlockCypher API: {response.StatusCode}");
                    throw new Exception("Failed to retrieve balance from BlockCypher API");
                }

                string jsonResponse = await response.Content.ReadAsStringAsync();
                JObject jsonResult = JObject.Parse(jsonResponse);

                decimal balance = jsonResult["balance"].Value<decimal>() / 100000000; // Convert from satoshis to BTC
                return balance;
            }
        }

        private static void RandomizeRequestHeaders(HttpClient httpClient)
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(GetRandomUserAgent());
            httpClient.DefaultRequestHeaders.Accept.ParseAdd(GetRandomAcceptHeader());
            httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd(GetRandomAcceptEncodingHeader());
            httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd(GetRandomAcceptLanguageHeader());
            httpClient.DefaultRequestHeaders.Connection.ParseAdd(GetRandomConnectionHeader());
            httpClient.DefaultRequestHeaders.Referrer = new Uri(GetRandomRefererHeader());
        }

        private static string GetRandomUserAgent() => GetRandomElement(new List<string>
    {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:93.0) Gecko/20100101 Firefox/93.0",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.63 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 11_5_1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.63 Safari/537.36"
    });

        private static string GetRandomAcceptHeader() => GetRandomElement(new List<string>
    {
        "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
        "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8",
        "*/*"
    });

        private static string GetRandomAcceptEncodingHeader() => GetRandomElement(new List<string>
    {
        "gzip, deflate, br",
        "gzip, deflate",
        "identity"
    });

        private static string GetRandomAcceptLanguageHeader() => GetRandomElement(new List<string>
    {
        "en-US,en;q=0.5",
        "en-US;q=0.8,en;q=0.7",
        "en;q=0.8"
    });

        private static string GetRandomConnectionHeader() => GetRandomElement(new List<string>
    {
        "keep-alive",
        "close"
    });

        private static string GetRandomRefererHeader() => GetRandomElement(new List<string>
    {
        "https://www.google.com/",
        "https://www.bing.com/",
        "https://www.yahoo.com/",
        "https://www.duckduckgo.com/"
    });

        private static string GetRandomElement(IList<string> elements)
        {
            var random = new Random();
            int index = random.Next(elements.Count);
            return elements[index];
        }

        public static async Task<IDisposable> ConnectToVPNAsync(string configPath, string username, string password)
        {
            string authFilePath = Path.GetTempFileName();
            File.WriteAllText(authFilePath, $"{username}\n{password}");

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "openvpn",
                ArgumentList = { "--config", configPath, "--auth-user-pass", authFilePath },
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Process process = new Process { StartInfo = startInfo };

            try
            {
                process.Start();
                Console.WriteLine("Connecting to VPN...");

                string line;
                while ((line = await process.StandardOutput.ReadLineAsync()) != null)
                {
                    Console.WriteLine(line);
                    if (line.Contains("Initialization Sequence Completed"))
                    {
                        Console.WriteLine("Connected to VPN.");
                        break;
                    }
                }
                return new ProcessDisposer(process, authFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while connecting to VPN: {ex.Message}");
                throw;
            }
        }
        private class ProcessDisposer : IDisposable
        {
            private readonly Process _process;
            private readonly string _authFilePath;

            public ProcessDisposer(Process process, string authFilePath)
            {
                _process = process;
                _authFilePath = authFilePath;
            }

            public void Dispose()
            {
                try
                {
                    _process.Kill();
                    _process.WaitForExit();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while disconnecting from VPN: {ex.Message}");
                }
                finally
                {
                    File.Delete(_authFilePath);
                }
            }
        }
    }
}
//using System;

//namespace CryptoSoft
//{
//    public static class Program
//    {
//        public static void Main(string[] args)
//        {
//            Console.WriteLine("CryptoSoft : Chiffrement de fichiers avec CryptoSoft.exe");
//            try
//            {
//                if (args.Length < 2)
//                {
//                    Console.WriteLine("Deux arguments requis : source et destination.");
//                    Environment.Exit(-1);
//                }

//                string source = args[0];
//                string key = args[1];



//                var fileManager = new FileManager(source, key);
//                long elapsedTime = fileManager.TransformFile();
//                //Console.WriteLine($"long elapsedTime: {elapsedTime}");

//                int time = unchecked((int)elapsedTime);

//                //Console.WriteLine($"int elapsedTime: {time}");

//                // Retourne le temps de chiffrement en ms comme code de sortie
//                Environment.Exit(time);
//            }
//            catch (Exception e)
//            {
//                Console.WriteLine($"Erreur CryptoSoft : {e.Message}");
//                Environment.Exit(-99); // code d'erreur générique
//            }
//        }
//    }
//}

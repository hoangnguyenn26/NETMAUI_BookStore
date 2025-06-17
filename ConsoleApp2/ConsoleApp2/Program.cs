using System;

namespace ConsoleApp2
{
    class Program
    {
        static void Main(string[] args)
        {
            var password = "Password123!";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, "$2a$11$hBqPh187JFDfmgUxrk4ZJeq7IyuBoLtOPugz.Di9Mc6weUeSDLYcy");
            // Hiển thị kết quả
            Console.WriteLine($"Password: {password}");
            Console.WriteLine($"Hashed password: {hashedPassword}");
            Console.WriteLine(isPasswordValid);
            // Giữ cho console mở để xem kết quả
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}

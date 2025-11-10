// 📁 Core/Backup/EncryptionService.cs
// ===== شروع کد =====

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TradingJournal.Core.Backup
{
    public partial class BackupService
    {
        private const int SaltSize = 32;
        private const int KeySize = 32;
        private const int Iterations = 100000;
        
        /// <summary>
        /// رمزنگاری فایل با AES
        /// </summary>
        private async Task EncryptFileAsync(string inputFile, string password)
        {
            var outputFile = inputFile + ".enc";
            
            // تولید salt تصادفی
            var salt = GenerateRandomBytes(SaltSize);
            
            // تولید کلید از پسورد
            var key = DeriveKey(password, salt);
            
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.GenerateIV();
                
                using (var inputStream = File.OpenRead(inputFile))
                using (var outputStream = File.Create(outputFile))
                {
                    // نوشتن salt و IV در ابتدای فایل
                    await outputStream.WriteAsync(salt, 0, salt.Length);
                    await outputStream.WriteAsync(aes.IV, 0, aes.IV.Length);
                    
                    // رمزنگاری و نوشتن داده‌ها
                    using (var cryptoStream = new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        await inputStream.CopyToAsync(cryptoStream);
                        cryptoStream.FlushFinalBlock();
                    }
                }
            }
            
            // جایگزینی فایل اصلی با فایل رمزنگاری شده
            File.Delete(inputFile);
            File.Move(outputFile, inputFile);
        }
        
        /// <summary>
        /// رمزگشایی فایل
        /// </summary>
        private async Task<string> DecryptFileAsync(string inputFile, string password)
        {
            var outputFile = inputFile.Replace(".enc", "");
            if (outputFile == inputFile)
            {
                outputFile = Path.Combine(
                    Path.GetDirectoryName(inputFile),
                    Path.GetFileNameWithoutExtension(inputFile) + "_decrypted" + Path.GetExtension(inputFile)
                );
            }
            
            using (var inputStream = File.OpenRead(inputFile))
            {
                // خواندن salt و IV
                var salt = new byte[SaltSize];
                var iv = new byte[16]; // AES IV size
                
                await inputStream.ReadAsync(salt, 0, SaltSize);
                await inputStream.ReadAsync(iv, 0, 16);
                
                // تولید کلید از پسورد
                var key = DeriveKey(password, salt);
                
                using (var aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    
                    using (var cryptoStream = new CryptoStream(inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read))
                    using (var outputStream = File.Create(outputFile))
                    {
                        await cryptoStream.CopyToAsync(outputStream);
                    }
                }
            }
            
            return outputFile;
        }
        
        /// <summary>
        /// تولید کلید از پسورد با استفاده از PBKDF2
        /// </summary>
        private byte[] DeriveKey(string password, byte[] salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(
                Encoding.UTF8.GetBytes(password),
                salt,
                Iterations,
                HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(KeySize);
            }
        }
        
        /// <summary>
        /// تولید بایت‌های تصادفی
        /// </summary>
        private byte[] GenerateRandomBytes(int size)
        {
            var bytes = new byte[size];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return bytes;
        }
        
        /// <summary>
        /// بررسی قدرت رمز عبور
        /// </summary>
        public static PasswordStrength CheckPasswordStrength(string password)
        {
            if (string.IsNullOrEmpty(password))
                return PasswordStrength.VeryWeak;
            
            var score = 0;
            
            // طول رمز
            if (password.Length >= 8) score++;
            if (password.Length >= 12) score++;
            if (password.Length >= 16) score++;
            
            // حروف بزرگ
            if (System.Text.RegularExpressions.Regex.IsMatch(password, @"[A-Z]"))
                score++;
            
            // حروف کوچک
            if (System.Text.RegularExpressions.Regex.IsMatch(password, @"[a-z]"))
                score++;
            
            // اعداد
            if (System.Text.RegularExpressions.Regex.IsMatch(password, @"\d"))
                score++;
            
            // کاراکترهای خاص
            if (System.Text.RegularExpressions.Regex.IsMatch(password, @"[!@#$%^&*(),.?""{}|<>]"))
                score++;
            
            return score switch
            {
                >= 7 => PasswordStrength.VeryStrong,
                >= 5 => PasswordStrength.Strong,
                >= 3 => PasswordStrength.Medium,
                >= 1 => PasswordStrength.Weak,
                _ => PasswordStrength.VeryWeak
            };
        }
    }
    
    public enum PasswordStrength
    {
        VeryWeak,
        Weak,
        Medium,
        Strong,
        VeryStrong
    }
}

// ===== پایان کد =====
using System;
using System.ComponentModel.DataAnnotations;

namespace InstagramAPI.Models
{
    /// <summary>
    /// Instagram gönderilerinin altındaki yorumları temsil eden sınıf
    /// </summary>
    public class Comment
    {
        /// <summary>
        /// Yorumun benzersiz kimliği
        /// </summary>
        [Required(ErrorMessage = "Yorum ID'si gereklidir")]
        public string Id { get; set; }

        /// <summary>
        /// Yorumun metni
        /// </summary>
        [Required(ErrorMessage = "Yorum metni gereklidir")]
        [MaxLength(2200, ErrorMessage = "Yorum metni 2200 karakterden uzun olamaz")]
        public string Text { get; set; }

        /// <summary>
        /// Yorumun yazılma tarihi
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Yorumu yazan kullanıcının adı
        /// </summary>
        [Required(ErrorMessage = "Yorum sahibinin kullanıcı adı gereklidir")]
        public string OwnerName { get; set; }

        /// <summary>
        /// Yorumu yazan kullanıcının profil fotoğrafı URL'i
        /// </summary>
        public string OwnerPicture { get; set; }
    }
}

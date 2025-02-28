using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace InstagramAPI.Models
{
    /// <summary>
    /// Instagram gönderilerini temsil eden sınıf
    /// </summary>
    public class Post
    {
        /// <summary>
        /// Gönderinin benzersiz kimliği
        /// </summary>
        [Required(ErrorMessage = "Gönderi ID'si gereklidir")]
        public string Id { get; set; }

        /// <summary>
        /// Gönderinin tam boy resim URL'i
        /// </summary>
        [Required(ErrorMessage = "Gönderi resmi gereklidir")]
        [Url(ErrorMessage = "Geçerli bir resim URL'i girilmelidir")]
        public string Picture { get; set; }

        /// <summary>
        /// Gönderinin küçük boy önizleme resmi URL'i
        /// </summary>
        [Url(ErrorMessage = "Geçerli bir önizleme URL'i girilmelidir")]
        public string Thumbnail { get; set; }

        /// <summary>
        /// Gönderi açıklaması (caption)
        /// </summary>
        [MaxLength(2200, ErrorMessage = "Gönderi açıklaması 2200 karakterden uzun olamaz")]
        public string Caption { get; set; }

        /// <summary>
        /// Gönderinin video olup olmadığını belirtir
        /// </summary>
        public bool isVideo { get; set; }

        /// <summary>
        /// Gönderinin kısa kodu (URL'de kullanılır)
        /// </summary>
        [Required(ErrorMessage = "Gönderi kısa kodu gereklidir")]
        public string ShortCode { get; set; }

        /// <summary>
        /// Gönderinin yorum sayısı
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "Yorum sayısı negatif olamaz")]
        public int CommentCount { get; set; }

        /// <summary>
        /// Gönderinin beğeni sayısı
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "Beğeni sayısı negatif olamaz")]
        public int LikeCount { get; set; }

        /// <summary>
        /// Gönderinin paylaşım tarihi
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Gönderinin tam URL'i
        /// </summary>
        [Required(ErrorMessage = "Gönderi URL'i gereklidir")]
        [Url(ErrorMessage = "Geçerli bir gönderi URL'i girilmelidir")]
        public string PostUrl { get; set; }
    }
}

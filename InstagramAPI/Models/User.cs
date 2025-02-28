using System;
using System.ComponentModel.DataAnnotations;

namespace InstagramAPI.Models
{
    /// <summary>
    /// Instagram kullanıcılarını temsil eden sınıf
    /// </summary>
    public class User
    {
        /// <summary>
        /// Kullanıcının benzersiz kimliği
        /// </summary>
        [Required(ErrorMessage = "Kullanıcı ID'si gereklidir")]
        public string Id { get; set; }

        /// <summary>
        /// Kullanıcının Instagram kullanıcı adı
        /// </summary>
        [Required(ErrorMessage = "Kullanıcı adı gereklidir")]
        [RegularExpression(@"^[a-zA-Z0-9_.]+$", ErrorMessage = "Kullanıcı adı sadece harf, rakam, nokta ve alt çizgi içerebilir")]
        public string UserName { get; set; }

        /// <summary>
        /// Kullanıcının tam adı
        /// </summary>
        [MaxLength(100, ErrorMessage = "Tam ad 100 karakterden uzun olamaz")]
        public string FullName { get; set; }

        /// <summary>
        /// Kullanıcının profil biyografisi
        /// </summary>
        [MaxLength(150, ErrorMessage = "Biyografi 150 karakterden uzun olamaz")]
        public string Biography { get; set; }

        /// <summary>
        /// Kullanıcının profil fotoğrafı URL'i
        /// </summary>
        [Url(ErrorMessage = "Geçerli bir profil fotoğrafı URL'i girilmelidir")]
        public string ProfilePicture { get; set; }

        /// <summary>
        /// Kullanıcının yüksek çözünürlüklü profil fotoğrafı URL'i
        /// </summary>
        [Url(ErrorMessage = "Geçerli bir HD profil fotoğrafı URL'i girilmelidir")]
        public string ProfilePictureHD { get; set; }

        /// <summary>
        /// Kullanıcının takipçi sayısı
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "Takipçi sayısı negatif olamaz")]
        public int FollowerCount { get; set; }

        /// <summary>
        /// Kullanıcının takip ettiği hesap sayısı
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "Takip edilen sayısı negatif olamaz")]
        public int FollowCount { get; set; }

        /// <summary>
        /// Kullanıcının toplam gönderi sayısı
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "Gönderi sayısı negatif olamaz")]
        public int PostCount { get; set; }
    }
}

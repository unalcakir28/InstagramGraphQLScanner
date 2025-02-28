# InstagramGraphQLScanner

Instagram'ın public GraphQL API servisini kullanarak kullanıcı, post ve yorum verilerini çekebilen .NET 8 kütüphanesi.

## Özellikler
- Kullanıcı profil bilgilerini getirme
- Kullanıcının gönderilerini getirme
- Gönderi yorumlarını getirme
- Hashtag'e göre gönderi arama
- Sayfalama desteği (pagination)
- Instagram cookie yönetimi
- Herkese açık profil ve gönderilere erişim

## Gereksinimler
- .NET 8.0 veya üzeri
- Google Chrome (en son sürüm)
- Selenium WebDriver

## Kurulum

```bash
dotnet add package InstagramGraphQLScanner
```

## Kullanım Örnekleri

### 1. Temel Kullanım

```csharp
using InstagramAPI;

var cookies = getCookies();
var instagram = new InstagramAPI.Functions();

// Kullanıcı bilgilerini getirme
var user = instagram.GetUser("instagram", cookies);
Console.WriteLine($"Kullanıcı: {user.Username}");
Console.WriteLine($"Takipçi: {user.FollowerCount}");
Console.WriteLine($"Takip: {user.FollowingCount}");

// Kullanıcının gönderilerini getirme (ilk 20 gönderi, 2 sayfa)
var posts = instagram.GetPostsFromUserId(user.Id, cookies, postPerPage: 20, pageCount: 2);
foreach (var post in posts)
{
    Console.WriteLine($"Gönderi: {post.ShortCode}");
    Console.WriteLine($"Beğeni: {post.LikeCount}");
    Console.WriteLine($"Yorum: {post.CommentCount}");
}
```

### 2. Gönderi Yorumlarını Getirme

```csharp
// Belirli bir gönderinin yorumlarını getirme
var comments = instagram.GetComments("ABC123", cookies, postPerPage: 20, pageCount: 3);
foreach (var comment in comments)
{
    Console.WriteLine($"Kullanıcı: {comment.Owner.Username}");
    Console.WriteLine($"Yorum: {comment.Text}");
}
```

### 3. Hashtag Araması

```csharp
// Hashtag'e göre gönderi arama
var tagPosts = instagram.GetPostsFromTag("turkey", cookies, postPerPage: 10, pageCount: 3);
foreach (var post in tagPosts)
{
    Console.WriteLine($"URL: {post.Url}");
    Console.WriteLine($"Sahip: {post.Owner.Username}");
}
```

## Dönen Veri Tipleri

### User Model
```csharp
public class User
{
    public string Id { get; set; }
    public string Username { get; set; }
    public string FullName { get; set; }
    public string Biography { get; set; }
    public string ProfilePicUrl { get; set; }
    public int FollowerCount { get; set; }
    public int FollowingCount { get; set; }
    public int PostCount { get; set; }
    public bool IsPrivate { get; set; }
}
```

### Post Model
```csharp
public class Post
{
    public string Id { get; set; }
    public string ShortCode { get; set; }
    public string Url { get; set; }
    public string DisplayUrl { get; set; }
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public DateTime PostedAt { get; set; }
    public User Owner { get; set; }
}
```

## Önemli Notlar

1. Bu kütüphane yalnızca herkese açık profiller veya size açık olan profillerde çalışır
2. Private hesapların verilerine erişemezsiniz
3. Instagram'ın rate limit kurallarına dikkat edilmelidir
4. Çok fazla istek atılması durumunda Instagram geçici olarak IP adresinizi engelleyebilir
5. Cookie'lerin güncel olduğundan emin olun

## Hata Yönetimi

```csharp
try
{
    var user = instagram.GetUser("nonexistent_user", cookies);
}
catch (InstagramException ex)
{
    Console.WriteLine($"Hata: {ex.Message}");
}
```

## Katkıda Bulunma

1. Bu repository'yi fork edin
2. Feature branch'i oluşturun (`git checkout -b feature/amazing-feature`)
3. Değişikliklerinizi commit edin (`git commit -m 'Add some amazing feature'`)
4. Branch'inizi push edin (`git push origin feature/amazing-feature`)
5. Pull Request oluşturun

## Lisans

MIT License - Detaylar için LICENSE dosyasına bakın.


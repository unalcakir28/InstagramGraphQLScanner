# InstagramGraphQLScanner

Bilgisayarda Google Chrome son sürüm yüklü ve projenin nuget paketleri içerisindeki Selenium kütüphanesinin güncel olması gerekiyor. Aksi halde hata alır.

# Örnek kullanım

<pre>
<code class='language-cs'>
  static async Task Main(string[] args)
  {
      var cookies = getCookies();
      InstagramAPI.Functions instagramFunc = new InstagramAPI.Functions();
      var user = instagramFunc.GetUser("setup34digital", cookies);
      var userPosts = instagramFunc.GetPostsFromUserId(user.Id, cookies, postPerPage: 20, pageCount: 2);
      var postComments = instagramFunc.GetComments(userPosts[0].ShortCode, cookies, postPerPage: 20, pageCount: 3);
      var tagPosts = instagramFunc.GetPostsFromTag("pazar", cookies, postPerPage: 10, pageCount: 3);
  }
</code>
</pre>


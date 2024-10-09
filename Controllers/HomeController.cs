using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Twitter.Models;

namespace Twitter.Controllers;

public class HomeController : Controller
{
    string connectionString = "";

    public bool CheckLogin()
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("nickname")))
        {
            return false;
        }

        return true;
    }

    public string TokenUret(int userId)
    {
        var token = Guid.NewGuid().ToString();

        using var connection = new SqlConnection(connectionString);
        var sql = "INSERT INTO pwResetToken (UserId, Token, Created, Used) VALUES (@UserId, @Token, GETDATE(), 0)";
        connection.Execute(sql, new { UserId = userId, Token = token });

        return token;
    }

    public Register GetMail(string email)
    {
        using var connection = new SqlConnection(connectionString);
        var sql = "SELECT * FROM users WHERE Mail = @Mail";
        return connection.QueryFirstOrDefault<Register>(sql, new { Mail = email });
    }

    public int? UserIdGetir(string nickname)
    {
        using var connection = new SqlConnection(connectionString);
        var sql = "SELECT Id FROM users WHERE Nickname = @nickname";
        var userId = connection.QueryFirstOrDefault<int?>(sql, new { Nickname = nickname });
        return userId;
    }

    public bool TweetVarMi(int id)
    {
        using var connection = new SqlConnection(connectionString);
        var sql = "SELECT COUNT(1) FROM tweets WHERE Id = @id";
        var count = connection.ExecuteScalar<int>(sql, new { Id = id });

        return count > 0;
    }

    public IActionResult Index()
    {
        ViewData["Nickname"] = HttpContext.Session.GetString("nickname");
        using var connection = new SqlConnection(connectionString);
        var sql =
            "SELECT tweets.Id, Detail, users.Username as Username, tweets.CreatedDate, users.Nickname as Nickname, users.ImgUrl as ImgUrl, COUNT(comments.Id) as YorumSayisi FROM tweets LEFT JOIN users on tweets.UserId = users.Id LEFT JOIN comments on comments.TweetId = tweets.Id WHERE Visibility = 1 GROUP BY tweets.Id, Detail, users.Username, tweets.CreatedDate, users.Nickname,users.ImgUrl ORDER BY tweets.CreatedDate DESC;";
        var tweets = connection.Query<Tweet>(sql).ToList();

        return View(tweets);
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Register(Register? model)
    {
        if (model == null)
        {
            model = new Register();
        }

        return View(model);
    }

    [HttpPost]
    [Route("/Login")]
    public IActionResult Login(Login model)
    {
        if (!ModelState.IsValid)
        {
            TempData["AuthError"] = "Form eksik.";
            return RedirectToAction("Login");
        }

        model.Password = Helper.Hash(model.Password);
        using var connection = new SqlConnection(connectionString);
        var sql = "SELECT * FROM users WHERE Nickname = @Nickname AND Password = @Password";
        var user = connection.QueryFirstOrDefault<Login>(sql, new { model.Nickname, model.Password });

        if (user != null)
        {
            HttpContext.Session.SetInt32("userId", user.Id);
            HttpContext.Session.SetString("nickname", user.Nickname);
            ViewData["Nickname"] = HttpContext.Session.GetString("nickname");

            ViewBag.Message = "Giriş Başarılı";
            return View("Message");
        }

        TempData["AuthError"] = "Kullanıcı adı veya şifre hatalı";
        return View("Login");
    }

    [HttpPost]
    [Route("/KayitOl")]
    public IActionResult KayitOl(Register model)
    {
        if (!ModelState.IsValid)
        {
            TempData["AuthError"] = "Form eksik veya hatalı.";
            return View("Register");
        }

        if (model.Password != model.Pwconfirmend)
        {
            TempData["AuthError"] = "Şifreler Uyuşmuyor.";
            return View("Register", model);
        }

        using (var control = new SqlConnection(connectionString))
        {
            control.Open();
            var cntrl = "SELECT * FROM users WHERE Nickname = @Nickname";
            var user = control.QueryFirstOrDefault(cntrl, new { Nickname = model.Nickname });
            if (user != null)
            {
                TempData["AuthError"] = "Bu kullanıcı adı mevcut!.";
                return View("Register", model);
            }
        }

        model.ImgUrl = "/uploads/basic.png";
        model.RoleId = 1;
        model.Created = DateTime.Now;
        model.Updated = DateTime.Now;
        model.Password = Helper.Hash(model.Password);

        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();
            var sql =
                "INSERT INTO users (Username, Password, Mail, RoleId, Created, Updated, Nickname, ImgUrl) " +
                "VALUES (@Username, @Password, @Mail, @RoleId, @Created, @Updated, @Nickname, @ImgUrl)";
            var data = new
            {
                Username = model.Username,
                Password = model.Password,
                Mail = model.Mail,
                RoleId = model.RoleId,
                Created = model.Created,
                Updated = model.Updated,
                ImgUrl = model.ImgUrl,
                Nickname = model.Nickname
            };

            var rowAffected = connection.Execute(sql, data);
        }

        var client = new SmtpClient("")
        {
            Credentials = new NetworkCredential("", ""),
            EnableSsl = true
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress("postmaster@", "Twitter"),
            Subject = "Twitter Hoşgeldin mesajı",
            Body = $"Merhaba {model.Username}. Twitter Kaydınız başarılı bir şekilde tamamlanmıştır.",
            IsBodyHtml = true
        };

        mailMessage.To.Add(new MailAddress(model.Mail, model.Username));

        client.Send(mailMessage);

        ViewBag.Message = "Kayıt Başarılı";
        return View("Message");
    }


    public IActionResult Cikis()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index");
    }

    [Route("/profil/{nickname}")]
    public IActionResult Profile(string nickname)
    {
        ViewData["nickname"] = HttpContext.Session.GetString("nickname");
        int? userId = UserIdGetir(nickname);
        if (userId == null)
        {
            ViewBag.Message = "Böyle bir kullanıcı yok!";
            return View("Message");
        }

        var profil = new Profile();
        using (var connection = new SqlConnection(connectionString))
        {
            if (userId == HttpContext.Session.GetInt32("userId"))
            {
                ViewBag.profile = true;
                var sql =
                    "SELECT tweets.Id ,Detail, users.Username , CreatedDate, Visibility  FROM tweets LEFT JOIN users on tweets.UserId = users.Id WHERE UserId = @userId ORDER BY CreatedDate DESC";
                var tweets = connection.Query<Tweet>(sql, new { UserId = userId }).ToList();
                profil.Tweets = tweets;
            }
            else
            {
                ViewBag.profile = false;
                var sql =
                    "SELECT Detail, users.Username as Username, CreatedDate, Visibility  FROM tweets LEFT JOIN users on tweets.UserId = users.Id WHERE UserId = @userId AND Visibility = 1 ORDER BY CreatedDate DESC";
                var tweets = connection.Query<Tweet>(sql, new { UserId = userId }).ToList();
                profil.Tweets = tweets;
            }
        }

        using (var connection = new SqlConnection(connectionString))
        {
            var sql = "SELECT * FROM users WHERE Id = @userId";
            var profile = connection.QueryFirstOrDefault<Register>(sql, new { UserId = userId });
            profil.User = profile;
        }


        return View(profil);
    }

    [HttpPost]
    [Route("/AddTweet")]
    public IActionResult AddTweet(Tweet model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Message = "Eksik veya hatalı işlem yaptın.";
            return View("Message");
        }

        model.CreatedDate = DateTime.Now;

        // Kullanıcı ID'sini al ve kontrol et
        int? userIdFromSession = HttpContext.Session.GetInt32("userId");

        if (userIdFromSession == null)
        {
            ViewBag.Message = "Kullanıcı oturumu bulunamadı.";
            return View("Message");
        }

        model.UserId = userIdFromSession.Value;

        using var connection = new SqlConnection(connectionString);
        var sql =
            "INSERT INTO tweets (Detail, UserId, CreatedDate, Visibility) VALUES (@Detail, @UserId, @CreatedDate, @Visibility)";

        var data = new
        {
            Detail = model.Detail,
            UserId = model.UserId,
            CreatedDate = model.CreatedDate,
            Visibility = model.Visibility
        };

        var rowsAffected = connection.Execute(sql, data);

        return RedirectToAction("Index");
    }



    [Route("/tweet/{id}")]
    public IActionResult Tweet(int id)
    {
        if (!TweetVarMi(id))
        {
            ViewBag.Message = "Böyle bir tweet yok";
            return View("Message");
        }

        ViewData["Nickname"] = HttpContext.Session.GetString("nickname");

        ViewBag.AddYorum = CheckLogin();

        var detailTweet = new DetailTweet();

        using (var connection = new SqlConnection(connectionString))
        {
            var sql = "SELECT tweets.Id, UserId, Detail, users.Username AS Username, CreatedDate, users.Nickname AS Nickname, users.ImgUrl AS ImgUrl FROM tweets LEFT JOIN users ON tweets.UserId = users.Id WHERE tweets.Id = @id";
            var tweet = connection.QueryFirstOrDefault<Tweet>(sql, new { id });
            detailTweet.Tweet = tweet;
        }

        using (var connection = new SqlConnection(connectionString))
        {
            var sql = "SELECT comments.Id, UserId, Summary, users.Username, users.Nickname, users.ImgUrl, CreatedTime FROM comments LEFT JOIN users ON users.Id = comments.UserId WHERE TweetId = @id ORDER BY CreatedTime DESC";
            var comments = connection.Query<Comment>(sql, new { id }).ToList();
            detailTweet.Comments = comments;
        }

        if (detailTweet.Tweet != null && detailTweet.Tweet.UserId == HttpContext.Session.GetInt32("userId"))
        {
            ViewBag.yetki = "full";
        }

        ViewBag.id = HttpContext.Session.GetInt32("userId");

        return View(detailTweet);
    }



    [HttpPost]
    [Route("/addyorum")]
    public IActionResult AddYorum(Comment model)
    {
        if (!ModelState.IsValid)
        {
            // TODO: burada hata mesajı g�ster
            return RedirectToAction("Index");
        }

        model.CreatedTime = DateTime.Now;
        model.UserId = (int)HttpContext.Session.GetInt32("userId");

        using var connection = new SqlConnection(connectionString);
        var sql =
            "INSERT INTO comments (Summary, CreatedTime, UserId, TweetId) VALUES (@Summary, @CreatedTime, @UserId, @TweetId)";


        try
        {
            var affectedRows = connection.Execute(sql, model);

            using var cnt = new SqlConnection(connectionString);
            var cntsql =
                "SELECT users.Mail, tweets.Detail, users.Username FROM comments LEFT JOIN tweets on comments.TweetId = tweets.Id LEFT JOIN users on tweets.UserId = users.Id WHERE comments.TweetId = @TweetId";
            var tweetInfo = cnt.QueryFirstOrDefault<TweetInfo>(cntsql, new { TweetId = model.TweetId });

            using var reader = new StreamReader("wwwroot/mailTemp/mailtemp.html");
            var template = reader.ReadToEnd();
            var mailbody = template
                .Replace("{{Username}}", tweetInfo.Username)
                .Replace("{{TweetDetail}}", tweetInfo.Detail);


            var client = new SmtpClient("")
            {
                Credentials = new NetworkCredential("postmaster@", ""),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress("", "Twitter"),
                Subject = "Tweetinize bildirim var",
                Body = mailbody,
                IsBodyHtml = true
            };

            mailMessage.To.Add(new MailAddress(tweetInfo.Mail, tweetInfo.Username));

            client.Send(mailMessage);


            return RedirectToAction("Tweet", new { id = model.TweetId });
        }
        catch (Exception ex)
        {
            return RedirectToAction("Index");
        }
    }

    [Route("/YorumSil/{id}")]
    public IActionResult DeleteYorum(int id, int tweetId)
    {
        using var connection = new SqlConnection(connectionString);


        var sql = "DELETE FROM comments WHERE id = @Id";
        var rowsAffected = connection.Execute(sql, new { Id = id });

        return RedirectToAction("Tweet", new { id = tweetId });
    }

    [Route("/tweetsil/{id}")]
    public IActionResult TweetSil(int id, string nickname)
    {
        using var connection = new SqlConnection(connectionString);

        var sql = "DELETE FROM tweets WHERE id = @Id";
        var rowsAffected = connection.Execute(sql, new { Id = id });

        return RedirectToAction("Profile", new { nickname });
    }

    [HttpGet]
    [Route("/sifre-unuttum")]
    public IActionResult SifreUnuttum()
    {
        return View();
    }

    [HttpPost]
    [Route("/SifreUnuttum")]
    public IActionResult SifreUnuttum(string email)
    {
        using var connection = new SqlConnection(connectionString);

        var user = connection.QuerySingleOrDefault<Register>(
            "SELECT * FROM users WHERE Mail = @Mail", new { Mail = email });

        if (user == null)
        {
            ViewBag.Message = "Bu e-posta adresiyle kayıtlı bir kullanıcı bulunamadı.";
            return View("Message");
        }

        return RedirectToAction("PwResetLink", new { userId = user.Id });
    }

    public IActionResult PwResetLink(int userId)
    {
        using var connection = new SqlConnection(connectionString);
        string userEmail = connection.QueryFirstOrDefault<string>("SELECT Mail FROM users WHERE Id = @UserId", new { UserId = userId });

        var token = TokenUret(userId);

        var resetLink = Url.Action("ResetPassword", "Admin", new { token }, Request.Scheme);

        using var reader = new StreamReader("wwwroot/mailTemp/pwreset.html");
        var template = reader.ReadToEnd();
        var mailBody = template.Replace("{{Resetlink}}", resetLink);
        Debug.WriteLine("Gönderilen Bağlantı: " + resetLink);

        var client = new SmtpClient("")
        {
            Credentials = new NetworkCredential("", ""),
            EnableSsl = true
        };


        var mailMessage = new MailMessage
        {
            From = new MailAddress("", "Twitter"),
            Subject = "Şifre Sıfırlama Talebi",
            Body = mailBody,
            IsBodyHtml = true
        };

        mailMessage.To.Add(new MailAddress(userEmail));

        client.Send(mailMessage);

        ViewBag.Message = "Şifre sıfırlama mail olarak iletişmiştir.";
        return View("Message");
    }

    [HttpGet]
    public IActionResult Search()
    {
        return View(new AramaModel());
    }

    [HttpPost]
    public IActionResult Search(AramaModel model)
    {
        if (string.IsNullOrWhiteSpace(model.SearchTerm))
        {
            ModelState.AddModelError("", "Lütfen bir arama terimi girin.");
            return View(model);
        }

        using var connection = new SqlConnection(connectionString);
        var sql = "SELECT Id, Username, Nickname FROM users WHERE Username LIKE @SearchTerm OR Nickname LIKE @SearchTerm";

        var searchTerm = "%" + model.SearchTerm + "%";
        model.Sonuc = connection.Query<Arama>(sql, new { SearchTerm = searchTerm }).ToList();

        return View(model);
    }

}
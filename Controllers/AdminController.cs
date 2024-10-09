using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Twitter.Models;

namespace Twitter.Controllers;

public class AdminController : Controller
{
    string connectionString = "";

    public bool CheckLoginn()
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("nickname")))
        {
            return false;
        }

        return true;
    }

    public int? UserIdGetirr(string nickname)
    {
        using var connection = new SqlConnection(connectionString);
        var sql = "SELECT Id FROM users WHERE Nickname = @nickname";
        var userId = connection.QueryFirstOrDefault<int?>(sql, new { Nickname = nickname });
        return userId;
    }

    [Route("/duzenle/{nickname}")]
    public IActionResult Duzenle(string nickname)
    {
        ViewData["Nickname"] = HttpContext.Session.GetString("nickname");

        var checkLogin = CheckLoginn();
        if (!checkLogin)
        {
            ViewBag.Message = "Login Ol.";
            return View("Msg");
        }

        int? userId = UserIdGetirr(nickname);
        if (userId != HttpContext.Session.GetInt32("userId"))
        {
            ViewBag.Message = "What Are u Doing?.";
            return View("Msg");
        }

        using var connection = new SqlConnection(connectionString);
        var sql = "SELECT * FROM users WHERE Id = @userId";
        var users = connection.QueryFirstOrDefault<Register>(sql, new { UserId = userId });

        return View(users);
    }

    [HttpPost]
    [Route("/duzenlepw/{id}")]
    public IActionResult DuzenlePw(Register model)
    {
        if (string.IsNullOrEmpty(model.Password))
        {
            return RedirectToAction("Duzenle", new { model.Nickname });
        }
        using var connection = new SqlConnection(connectionString);
        var sql =
            "UPDATE users SET Password = @Password WHERE id = @Id";

        model.Password = Helper.Hash(model.Password);

        var data = new
        {
            model.Username,
            model.Password,
            model.ImgUrl,
            model.Id
        };

        var rowAffected = connection.Execute(sql, data);

        ViewBag.Message = "Şifre Güncellendi.";
        return View("Msg");

    }

    [HttpPost]
    [Route("/duzenleun/{id}")]
    public IActionResult DuzenleUn(Register model)
    {
        if (string.IsNullOrEmpty(model.Username))
        {
            return RedirectToAction("Duzenle", new { model.Nickname });
        }
        using var connection = new SqlConnection(connectionString);
        var sql =
            "UPDATE users SET Username = @Username WHERE id = @Id";

        var data = new
        {
            model.Username,
            model.Password,
            model.ImgUrl,
            model.Id
        };

        var rowAffected = connection.Execute(sql, data);

        ViewBag.Message = "Username Güncellendi.";
        return View("Msg");

    }

    [HttpPost]
    [Route("/duzenlepp/{id}")]
    public IActionResult DuzenlePp(Register model)
    {
        if (model.Image == null || model.Image.Length == 0)
        {
            return RedirectToAction("Duzenle", new { model.Nickname });
        }

        using var connection = new SqlConnection(connectionString);
        var sql = "UPDATE users SET ImgUrl = @ImgUrl WHERE id = @Id";
        var imageName = Guid.NewGuid() + Path.GetExtension(model.Image.FileName);
        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var path = Path.Combine(uploadsFolder, imageName);

        try
        {
            using var stream = new FileStream(path, FileMode.Create);
            model.Image.CopyTo(stream);
            model.ImgUrl = $"/uploads/{imageName}";
        }
        catch (Exception ex)
        {
            ViewBag.Message = "Dosya kaydedilirken bir hata oluştu: " + ex.Message;
            return View("Error"); 
        }

        var data = new
        {
            model.Username,
            model.Password,
            model.ImgUrl,
            model.Id
        };

        var rowAffected = connection.Execute(sql, data);

        ViewBag.Message = "Foto Güncellendi.";
        return View("Msg");
    }



    [HttpGet]
    public IActionResult ResetPassword(string token)
    {
        using var connection = new SqlConnection(connectionString);
        var resetToken = connection.QuerySingleOrDefault<ResetPwToken>(
            "SELECT * FROM PwResetToken WHERE Token = @Token AND Used = 0", new { Token = token });

        if (resetToken == null)
        {
            ViewBag.Message = "Geçersiz veya kullanılmış token";
            return View("Msg");
        }

        return View(new PwReset { Token = token });
    }

    [HttpPost]
    public IActionResult ResetPassword(PwReset model)
    {
        if (!ModelState.IsValid) return View(model);

        using var connection = new SqlConnection(connectionString);
        var resetToken = connection.QueryFirstOrDefault<ResetPwToken>(
            "SELECT * FROM pwResetToken WHERE Token = @Token AND Used = 0", new { model.Token });

        if (resetToken == null)
        {
            ViewBag.Message = "Geçersiz veya kullanılmış token";
            return View("Msg");
        }
        model.Pw = Helper.Hash(model.Pw);

        connection.Execute(
            "UPDATE users SET Password = @Password WHERE Id = @UserId",
            new { Password = model.Pw, resetToken.UserId }
        );

        connection.Execute(
            "UPDATE pwResetToken SET Used = 1 WHERE Id = @Id",
            new { resetToken.Id }
        );

        ViewBag.Message = "Şifre Başarılı bir şekilde değiştirildi";
        return View("Msg");
    }


}
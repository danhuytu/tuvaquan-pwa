using Microsoft.AspNetCore.Mvc;

namespace guithu.Controllers;

/// <summary>
/// Controller nền cho toàn bộ trang của ứng dụng.
/// Các controller kế thừa lớp này sẽ tự có thông tin chung của PWA trong ViewData.
/// </summary>
public abstract class BaseController : Controller
{
    protected BaseController()
    {
        ViewData["AppName"] = "Tú và Quân";
        ViewData["AppShortName"] = "Tú & Quân";
    }
}

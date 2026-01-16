using Microsoft.AspNetCore.Mvc;
using TUYAK.Models;
using System.Threading.Tasks;

namespace TUYAK.Controllers
{
    public class HomeController : Controller
    {
        private readonly HiraService _hiraService;

        public HomeController(HiraService hiraService) { _hiraService = hiraService; }

        public IActionResult Index() { return View(); }

        [HttpPost]
        public async Task<IActionResult> RequestAuth(string jumin)
        {
            string res = await _hiraService.RequestAuthAsync(jumin);
            string authNo = _hiraService.ExtractAuthNumber(res);
            return Json(new { success = true, debugAuthNo = authNo });
        }

        [HttpPost]
        public async Task<IActionResult> RunTest(string testType, string jumin, string authNo)
        {
            DurResponse res = new DurResponse { Success = false };

            switch (testType)
            {
                case "COM_History":
                    res = _hiraService.RunComMethodLegacy(jumin, "CheckMediHistory"); break;
                case "COM_List":
                    res = _hiraService.RunComMethodLegacy(jumin, "CheckMediHistoryList"); break;
                case "COM_DataList":
                    res = _hiraService.RunComMethodLegacy(jumin, "CheckMediDataList"); break;

                case "NORMAL_B":
                    res = await _hiraService.RunSocketTestAsync(jumin, authNo, "6", "B"); break;
                case "NORMAL_S":
                    res = await _hiraService.RunSocketTestAsync(jumin, authNo, "6", "S"); break;

                case "EMERG_B":
                    res = await _hiraService.RunSocketTestAsync(jumin, "", "3", "B"); break;
                case "EMERG_S":
                    res = await _hiraService.RunSocketTestAsync(jumin, "", "3", "S"); break;
            }

            return Json(res);
        }
    }
}
using Microsoft.AspNetCore.Mvc;
using TUYAK.Models;
using System.Threading.Tasks;

namespace TUYAK.Controllers
{
    // DUR 테스트/데모용 홈 컨트롤러
    public class HomeController : Controller
    {
        private readonly HiraService _hiraService; // DUR 서비스 주입

        public HomeController(HiraService hiraService) { _hiraService = hiraService; }

        public IActionResult Index() { return View(); } // 기본 페이지

        [HttpPost]
        public async Task<IActionResult> RequestAuth(string jumin)
        {
            // 인증 요청 후 응답에서 5자리 인증번호 추출
            string res = await _hiraService.RequestAuthAsync(jumin);
            string authNo = _hiraService.ExtractAuthNumber(res);
            return Json(new { success = true, debugAuthNo = authNo }); // 디버그용 반환
        }

        [HttpPost]
        public async Task<IActionResult> RunTest(string testType, string jumin, string authNo)
        {
            // 테스트 유형별 COM/소켓 호출 분기
            DurResponse res = new DurResponse { Success = false };

            switch (testType)
            {
                case "COM_History":
                    res = _hiraService.RunComMethodLegacy(jumin, "CheckMediHistory"); break;      // 이력 조회
                case "COM_List":
                    res = _hiraService.RunComMethodLegacy(jumin, "CheckMediHistoryList"); break;  // 이력 리스트 조회
                case "COM_DataList":
                    res = _hiraService.RunComMethodLegacy(jumin, "CheckMediDataList"); break;     // 데이터 리스트 조회

                case "NORMAL_B":
                    res = await _hiraService.RunSocketTestAsync(jumin, authNo, "6", "B"); break;  // 일반 DUR(B)
                case "NORMAL_S":
                    res = await _hiraService.RunSocketTestAsync(jumin, authNo, "6", "S"); break;  // 일반 DUR(S)

                case "EMERG_B":
                    res = await _hiraService.RunSocketTestAsync(jumin, "", "3", "B"); break;      // 응급 DUR(B)
                case "EMERG_S":
                    res = await _hiraService.RunSocketTestAsync(jumin, "", "3", "S"); break;      // 응급 DUR(S)
            }

            // 파싱 결과 그대로 반환
            return Json(res);
        }
    }
}
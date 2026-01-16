namespace WebApplication1.Models
{
    // 오류 페이지에서 사용되는 뷰 모델
    public class ErrorViewModel
    {
        public string? RequestId { get; set; } // 요청 식별자

        // RequestId가 있으면 화면에 표시
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// 코드페이지 인코딩(EUC-KR 등) 사용 가능하게 등록
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

// MVC 컨트롤러/뷰 등록
builder.Services.AddControllersWithViews();
// DUR 통신 서비스 DI 등록(요청 범위)
builder.Services.AddScoped<TUYAK.Models.HiraService>();

// 애플리케이션 빌드
var app = builder.Build();

// 운영 환경에서 예외 처리 페이지/HSTS 적용
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// 기본 미들웨어 파이프라인 구성
app.UseHttpsRedirection(); // HTTPS로 리다이렉트
app.UseStaticFiles();      // wwwroot 정적 파일 제공
app.UseRouting();          // 엔드포인트 라우팅 활성화
app.UseAuthorization();    // 권한 확인(필요 시)

// 기본 라우트: Home/Index
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
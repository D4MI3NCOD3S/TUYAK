using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TUYAK.Models
{
    // DUR 응답 표준 모델
    public class DurResponse
    {
        public bool Success { get; set; }      // 성공 여부
        public string Message { get; set; }    // 설명/오류 메시지
        public string ResultType { get; set; } // 응답 유형(URL/DATA/ERROR)
        public string Content { get; set; }    // 원본/요약 콘텐츠
        public List<string> DataList { get; set; } = new List<string>(); // 파싱된 데이터 항목
    }

    // DUR 통신/COM 연동 서비스
    public class HiraService
    {
        // 브로커 접속 정보
        private const string BROKER_IP = "127.0.0.1";
        private const int BROKER_PORT = 10001;
        private const string YOYANG_CODE = "10160044"; // 요양기관 코드

        public HiraService()
        {
            // EUC-KR 인코딩 사용을 위해 등록
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        // 레거시 COM 방식 호출(HiraDur.Client)
        public DurResponse RunComMethodLegacy(string jumin, string methodName)
        {
            string progId = "HiraDur.Client";

            object comObject = null;
            Type comType = null;

            try
            {
                // COM 타입 조회(버전별 ProgID 시도)
                comType = Type.GetTypeFromProgID(progId);

                if (comType == null)
                {
                    comType = Type.GetTypeFromProgID("HiraDur.Client.1");
                }

                if (comType == null)
                    return new DurResponse { Success = false, Message = $"COM 객체 '{progId}'를 찾을 수 없습니다. (x86 빌드 설정 확인 필요)" };

                // COM 객체 생성
                comObject = Activator.CreateInstance(comType);

                // 메서드별 인자 구성
                object[] args;
                if (methodName == "CheckMediDataList")
                {
                    // 데이터 리스트 조회 형식 인자
                    args = new object[] { null, jumin, YOYANG_CODE, "123456", "" };
                }
                else
                {
                    // 기본 조회 형식 인자
                    args = new object[] { jumin, YOYANG_CODE, "123456" };
                }

                // COM 메서드 호출
                object result = comType.InvokeMember(
                    methodName,
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    comObject,
                    args
                );

                string resultStr = result != null ? result.ToString() : "null";

                // 응답이 URL이면 팝업 링크로 간주, 그 외는 데이터 텍스트로 처리
                if (resultStr.Contains("http"))
                    return new DurResponse { Success = true, ResultType = "URL", Message = "팝업 URL 리턴됨", Content = resultStr };
                else
                    return new DurResponse { Success = true, ResultType = "DATA", Message = "데이터 수신 성공", Content = resultStr };
            }
            catch (Exception ex)
            {
                // COM 예외 처리
                return new DurResponse { Success = false, ResultType = "ERROR", Message = $"COM 오류 ({methodName}): {ex.Message}" };
            }
            finally
            {
                // COM 리소스 해제
                if (comObject != null && Marshal.IsComObject(comObject))
                {
                    Marshal.ReleaseComObject(comObject);
                }
            }
        }

        // 소켓 기반 DUR 테스트 호출
        public async Task<DurResponse> RunSocketTestAsync(string jumin, string authNo, string reqCode, string moduleType)
        {
            try
            {
                // 주민번호 숫자만 추출
                string cleanJumin = Regex.Replace(jumin ?? "", @"[^0-9]", "");

                // 요청 패킷 생성
                byte[] packet = BuildN1400Packet(cleanJumin, authNo, reqCode, moduleType);

                // 브로커로 송신 후 응답 수신
                string responseString = await SendSocketAsync(packet);

                if (string.IsNullOrEmpty(responseString))
                    return new DurResponse { Success = false, Message = "서버(Broker) 응답 없음" };

                // 응답 파싱 및 유형 판별
                return ParseN1400Response(responseString);
            }
            catch (Exception ex)
            {
                // 통신 오류
                return new DurResponse { Success = false, Message = "통신 에러: " + ex.Message };
            }
        }

        // 인증번호 요청(초기 인증 단계)
        public async Task<string> RequestAuthAsync(string jumin)
        {
            try
            {
                // 주민번호 정제 후 인증 요청 패킷 생성
                string cleanJumin = Regex.Replace(jumin ?? "", @"[^0-9]", "");
                byte[] packet = BuildN1400Packet(cleanJumin, "", "0", "B");
                return await SendSocketAsync(packet);
            }
            catch { return null; }
        }

        // 원시 응답에서 5자리 인증번호 추출
        public string ExtractAuthNumber(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            // 0x10(File Separator) 기준 필드 분리
            string[] f = raw.Split((char)0x10);
            // 뒤에서부터 5자리 숫자 형태 탐색
            for (int i = f.Length - 1; i >= 0; i--) { string v = f[i].Trim(); if (v.Length == 5 && long.TryParse(v, out _) ) return v; }
            return null;
        }

        // N1400 규격 패킷 생성
        private byte[] BuildN1400Packet(string jumin, string authNo, string reqCode, string moduleType)
        {
            Encoding encoding = Encoding.GetEncoding("euc-kr");
            const byte FS = 0x10; // 필드 구분자
            List<byte> body = new List<byte>();

            // 바디 필드 구성(프로토콜 규격에 맞는 순서/값)
            body.Add(0x01); body.AddRange(encoding.GetBytes("001")); body.AddRange(encoding.GetBytes("06"));
            body.AddRange(encoding.GetBytes(jumin)); body.Add(FS);
            body.AddRange(encoding.GetBytes(YOYANG_CODE)); body.Add(FS);
            body.AddRange(encoding.GetBytes("123456")); body.Add(FS); // 운영 인증키 자리(예시)
            body.AddRange(encoding.GetBytes(reqCode)); body.Add(FS);  // 요청 코드(예: 6 일반, 3 응급)
            body.AddRange(encoding.GetBytes((authNo ?? "").PadRight(5).Substring(0, 5))); body.Add(FS); // 인증번호(5자리)
            body.AddRange(encoding.GetBytes(moduleType)); body.Add(FS); // 모듈 타입(S/B)
            body.Add(0x01); // 종료 플래그

            // 헤더 프리픽스 + 바디 길이(Big-endian) + 바디 결합
            string headerPrefix = $"D1.100{YOYANG_CODE}N140000FFF";
            List<byte> finalPacket = new List<byte>();
            finalPacket.AddRange(encoding.GetBytes(headerPrefix));
            int len = body.Count;
            byte[] lenBytes = BitConverter.GetBytes(len);
            if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes); // Big-endian 변환
            finalPacket.AddRange(lenBytes);
            finalPacket.AddRange(body);

            return finalPacket.ToArray();
        }

        // N1400 응답 파싱(에러/URL/DATA 판별)
        private DurResponse ParseN1400Response(string rawBody)
        {
            var result = new DurResponse { Success = false, Content = rawBody };

            // 에러 문자열 포함 시 오류로 처리
            if (rawBody.Contains("오류") || rawBody.StartsWith("Error"))
            {
                result.Message = "DUR 서버 오류: " + rawBody; return result;
            }

            // 필드 분리
            string[] fields = rawBody.Split((char)0x10);

            // URL 포함 필드가 있으면 팝업 응답으로 처리
            foreach (var f in fields)
            {
                if (f.Contains("http"))
                {
                    result.ResultType = "URL"; result.Content = f.Trim();
                    result.Success = true; result.Message = "팝업 URL 수신"; return result;
                }
            }

            // 일반 데이터 응답 처리(특정 값 제외 필터링)
            if (fields.Length > 4 && !fields[0].Trim().Equals("N"))
            {
                result.ResultType = "DATA"; result.Success = true; result.Message = "텍스트 데이터 수신 성공";
                foreach (var f in fields)
                {
                    string val = f.Trim();
                    // 의미 없는 코드/플래그 제외
                    if (val.Length > 1 && !val.StartsWith("00") && val != "S" && val != "B")
                    {
                        result.DataList.Add(val);
                    }
                }
                result.Content = string.Join(" | ", result.DataList);
                return result;
            }

            // 조회 실패/거절 케이스
            result.Message = "조회 실패/거절 (응답: " + rawBody + ")";
            return result;
        }

        // 소켓 송수신(헤더 28바이트 후 바디 길이만큼 수신)
        private async Task<string> SendSocketAsync(byte[] packet)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    // 3초 타임아웃으로 브로커 접속
                    var connectTask = client.ConnectAsync(BROKER_IP, BROKER_PORT);
                    if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask) return null;
                    await connectTask;

                    using (NetworkStream stream = client.GetStream())
                    {
                        // 요청 패킷 전송
                        await stream.WriteAsync(packet, 0, packet.Length);
                        await stream.FlushAsync();

                        // 헤더(28바이트) 수신
                        byte[] head = new byte[28];
                        int headRead = 0;
                        while (headRead < 28)
                        {
                            int r = await stream.ReadAsync(head, headRead, 28 - headRead);
                            if (r == 0) return null; headRead += r; // 연결 종료 시 null
                        }

                        // 헤더 내 바디 길이 4바이트(Big-endian) 추출
                        byte[] sz = new byte[4]; Array.Copy(head, 24, sz, 0, 4);
                        if (BitConverter.IsLittleEndian) Array.Reverse(sz);
                        int bodySize = BitConverter.ToInt32(sz, 0);

                        // 바디 전체 수신
                        byte[] bodyBuffer = new byte[bodySize];
                        int total = 0;
                        while (total < bodySize)
                        {
                            int bytes = await stream.ReadAsync(bodyBuffer, total, bodySize - total);
                            if (bytes == 0) break; total += bytes;
                        }

                        // EUC-KR로 디코딩 후 문자열 반환
                        return Encoding.GetEncoding("euc-kr").GetString(bodyBuffer);
                    }
                }
            }
            catch (Exception ex)
            {
                // 디버그 로깅 후 null 반환
                Debug.WriteLine(ex.Message);
                return null;
            }
        }
    }
}
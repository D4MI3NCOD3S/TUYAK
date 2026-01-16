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
    public class DurResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ResultType { get; set; }
        public string Content { get; set; }
        public List<string> DataList { get; set; } = new List<string>();
    }

    public class HiraService
    {
        private const string BROKER_IP = "127.0.0.1";
        private const int BROKER_PORT = 10001;
        private const string YOYANG_CODE = "10160044";

        public HiraService()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public DurResponse RunComMethodLegacy(string jumin, string methodName)
        {
            string progId = "HiraDur.Client";

            object comObject = null;
            Type comType = null;

            try
            {
                comType = Type.GetTypeFromProgID(progId);

                if (comType == null)
                {
                    comType = Type.GetTypeFromProgID("HiraDur.Client.1");
                }

                if (comType == null)
                    return new DurResponse { Success = false, Message = $"COM 객체 '{progId}'를 찾을 수 없습니다. (x86 빌드 설정 확인 필요)" };

                comObject = Activator.CreateInstance(comType);

                object[] args;
                if (methodName == "CheckMediDataList")
                {
                    args = new object[] { null, jumin, YOYANG_CODE, "123456", "" };
                }
                else
                {
                    args = new object[] { jumin, YOYANG_CODE, "123456" };
                }

                object result = comType.InvokeMember(
                    methodName,
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    comObject,
                    args
                );

                string resultStr = result != null ? result.ToString() : "null";

                if (resultStr.Contains("http"))
                    return new DurResponse { Success = true, ResultType = "URL", Message = "팝업 URL 리턴됨", Content = resultStr };
                else
                    return new DurResponse { Success = true, ResultType = "DATA", Message = "데이터 수신 성공", Content = resultStr };
            }
            catch (Exception ex)
            {
                return new DurResponse { Success = false, ResultType = "ERROR", Message = $"COM 오류 ({methodName}): {ex.Message}" };
            }
            finally
            {
                if (comObject != null && Marshal.IsComObject(comObject))
                {
                    Marshal.ReleaseComObject(comObject);
                }
            }
        }

        public async Task<DurResponse> RunSocketTestAsync(string jumin, string authNo, string reqCode, string moduleType)
        {
            try
            {
                string cleanJumin = Regex.Replace(jumin ?? "", @"[^0-9]", "");

                byte[] packet = BuildN1400Packet(cleanJumin, authNo, reqCode, moduleType);

                string responseString = await SendSocketAsync(packet);

                if (string.IsNullOrEmpty(responseString))
                    return new DurResponse { Success = false, Message = "서버(Broker) 응답 없음" };

                return ParseN1400Response(responseString);
            }
            catch (Exception ex)
            {
                return new DurResponse { Success = false, Message = "통신 에러: " + ex.Message };
            }
        }

        public async Task<string> RequestAuthAsync(string jumin)
        {
            try
            {
                string cleanJumin = Regex.Replace(jumin ?? "", @"[^0-9]", "");
                byte[] packet = BuildN1400Packet(cleanJumin, "", "0", "B");
                return await SendSocketAsync(packet);
            }
            catch { return null; }
        }

        public string ExtractAuthNumber(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            string[] f = raw.Split((char)0x10);
            for (int i = f.Length - 1; i >= 0; i--) { string v = f[i].Trim(); if (v.Length == 5 && long.TryParse(v, out _)) return v; }
            return null;
        }

        private byte[] BuildN1400Packet(string jumin, string authNo, string reqCode, string moduleType)
        {
            Encoding encoding = Encoding.GetEncoding("euc-kr");
            const byte FS = 0x10;
            List<byte> body = new List<byte>();

            body.Add(0x01); body.AddRange(encoding.GetBytes("001")); body.AddRange(encoding.GetBytes("06"));
            body.AddRange(encoding.GetBytes(jumin)); body.Add(FS);
            body.AddRange(encoding.GetBytes(YOYANG_CODE)); body.Add(FS);
            body.AddRange(encoding.GetBytes("123456")); body.Add(FS);
            body.AddRange(encoding.GetBytes(reqCode)); body.Add(FS);
            body.AddRange(encoding.GetBytes((authNo ?? "").PadRight(5).Substring(0, 5))); body.Add(FS);
            body.AddRange(encoding.GetBytes(moduleType)); body.Add(FS);
            body.Add(0x01);

            string headerPrefix = $"D1.100{YOYANG_CODE}N140000FFF";
            List<byte> finalPacket = new List<byte>();
            finalPacket.AddRange(encoding.GetBytes(headerPrefix));
            int len = body.Count;
            byte[] lenBytes = BitConverter.GetBytes(len);
            if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
            finalPacket.AddRange(lenBytes);
            finalPacket.AddRange(body);

            return finalPacket.ToArray();
        }

        private DurResponse ParseN1400Response(string rawBody)
        {
            var result = new DurResponse { Success = false, Content = rawBody };

            if (rawBody.Contains("오류") || rawBody.StartsWith("Error"))
            {
                result.Message = "DUR 서버 오류: " + rawBody; return result;
            }

            string[] fields = rawBody.Split((char)0x10);

            foreach (var f in fields)
            {
                if (f.Contains("http"))
                {
                    result.ResultType = "URL"; result.Content = f.Trim();
                    result.Success = true; result.Message = "팝업 URL 수신"; return result;
                }
            }

            if (fields.Length > 4 && !fields[0].Trim().Equals("N"))
            {
                result.ResultType = "DATA"; result.Success = true; result.Message = "텍스트 데이터 수신 성공";
                foreach (var f in fields)
                {
                    string val = f.Trim();
                    if (val.Length > 1 && !val.StartsWith("00") && val != "S" && val != "B")
                    {
                        result.DataList.Add(val);
                    }
                }
                result.Content = string.Join(" | ", result.DataList);
                return result;
            }

            result.Message = "조회 실패/거절 (응답: " + rawBody + ")";
            return result;
        }

        private async Task<string> SendSocketAsync(byte[] packet)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    var connectTask = client.ConnectAsync(BROKER_IP, BROKER_PORT);
                    if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask) return null;
                    await connectTask;

                    using (NetworkStream stream = client.GetStream())
                    {
                        await stream.WriteAsync(packet, 0, packet.Length);
                        await stream.FlushAsync();

                        byte[] head = new byte[28];
                        int headRead = 0;
                        while (headRead < 28)
                        {
                            int r = await stream.ReadAsync(head, headRead, 28 - headRead);
                            if (r == 0) return null; headRead += r;
                        }

                        byte[] sz = new byte[4]; Array.Copy(head, 24, sz, 0, 4);
                        if (BitConverter.IsLittleEndian) Array.Reverse(sz);
                        int bodySize = BitConverter.ToInt32(sz, 0);

                        byte[] bodyBuffer = new byte[bodySize];
                        int total = 0;
                        while (total < bodySize)
                        {
                            int bytes = await stream.ReadAsync(bodyBuffer, total, bodySize - total);
                            if (bytes == 0) break; total += bytes;
                        }
                        return Encoding.GetEncoding("euc-kr").GetString(bodyBuffer);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }
    }
}
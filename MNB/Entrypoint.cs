using CSCore;
using CSCore.Codecs;
using OnsetDetection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MusicNoteBuilder {
    /// <summary>
    /// 진행 상황을 콘솔에 출력하는 기능을 노출하는 클래스입니다.
    /// </summary>
    class ConsoleNotifier : IProgress<string> {
        public void Report(string value) {
            Console.WriteLine(value);
        }
    }

    /// <summary>
    /// 값 별 위치를 나타냅니다.
    /// </summary>
    enum OnsetDirection : byte {
        /// <summary>
        /// 오른쪽입니다.
        /// </summary>
        R = 0,
        /// <summary>
        /// 왼쪽입니다.
        /// </summary>
        L = 1,
    }

    /// <summary>
    /// 프로그램 주 진입점을 노출하는 클래스입니다.
    /// </summary>
    class Entrypoint {
        static void Main(string[] args) {
            // Validate arguments 
            if (args.Length < 3) {
                Console.WriteLine("Usage: mnb <sensitivity> <sound file> <output> [upperBound]");
                return;
            }

            float sensitivity;
            try { sensitivity = float.Parse(args[0]); }
            catch {
                Console.WriteLine("ERROR: Invalid sensitivity.");
                return;
            }

            string input = args[1], output = args[2];
            if (!File.Exists(input)) {
                Console.WriteLine("ERROR: File not exist.");
                return;
            }

            StreamWriter sw;
            try { sw = new StreamWriter(output); }
            catch {
                Console.WriteLine("ERROR: Cannot create output file.");
                return;
            }

            DetectorOptions option = DetectorOptions.Default;
            option.ActivationThreshold = sensitivity;

            OnsetDetector detector;
            try { detector = new OnsetDetector(option, new ConsoleNotifier()); }
            catch {
                Console.WriteLine("ERROR: Could not create OnsetDetector instance.");
                sw.Dispose();
                return;
            }

            IWaveSource source;
            try {
                source = CodecFactory.Instance.GetCodec(input);
                if (source == null) throw new Exception();
            }
            catch {
                Console.WriteLine("ERROR: Could not load IWaveSource.");
                sw.Dispose();
                return;
            }

            ISampleSource sample;
            try { sample = source.ToSampleSource(); }
            catch {
                Console.WriteLine("ERROR: Could not convert IWaveSource to ISampleSource.");
                sw.Dispose();
                source.Dispose();
                return;
            }

            List<Onset> onsets = new List<Onset>();
            try
            {
                onsets = detector.Detect(sample);
            } catch {
                Console.WriteLine("ERROR: Could not detect onsets from ISampleSource.");
                sw.Dispose();
                source.Dispose();
                return;
            }

            // Write to output file;
            StringBuilder onsetList = new StringBuilder();

            // 진폭 제한
            int ampUpperBound = 50;
            if (args.Length == 4) {
                try { ampUpperBound = int.Parse(args[3]); }
                catch {
                    Console.WriteLine("ERROR: Invalid amplitude upper bound.");
                    sw.Dispose();
                    source.Dispose();
                    return;
                }
            }

            // 남은 것을 찾는다.
            int streak = 0;
            OnsetDirection current = (OnsetDirection) ((int)onsets[0].OnsetAmplitude % 2);

            for (int i = 0; i < onsets.Count; i++) {
                if (onsets[i].OnsetAmplitude > ampUpperBound) {
                    onsetList.AppendLine(string.Format("{0} U", (int)(onsets[i].OnsetTime * 1000)));
                    continue;
                }

                OnsetDirection pick = (OnsetDirection) ((int)onsets[i].OnsetAmplitude % 2);
                if (current != pick) streak = 0;
                else streak++;

                // 4개 이상(0부터 시작) 연속되면 바꾼다.
                if (streak >= 3) {
                    if (pick == OnsetDirection.L) pick = OnsetDirection.R;
                    else pick = OnsetDirection.L;
                    streak = 0;
                }

                onsetList.AppendLine(string.Format("{0} {1}", (int)(onsets[i].OnsetTime * 1000), pick));
                current = pick;
            }
            

            try { sw.Write(onsetList.ToString()); }
            catch {
                Console.WriteLine("ERROR: Cannot write result to output file.");
                sw.Dispose();
                source.Dispose();
                return;
            }

            try { sw.Flush(); }
            catch { Console.WriteLine("ERROR: Unable to flush into file."); }

            sw.Dispose();
            source.Dispose();
        }
    }
}

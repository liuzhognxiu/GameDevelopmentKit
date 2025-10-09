using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Text;

namespace AutoUI
{
	public static class LogUtil
	{
		private static bool hadWarnning;
		private static List<string> LogWarningList = new List<string>();
		private static string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "unity_ui_log.txt");
		private static bool isFirstWrite = true;
		public static void ClearLogFile()
		{
			try
			{
				// 清空文件内容
				File.WriteAllText(logPath, string.Empty);
				isFirstWrite = true; // 重置标志位，下次写入时会重新写入标题
				Console.WriteLine("日志文件已清空");
			}
			catch (Exception e)
			{
				Console.WriteLine($"[日志系统错误] 无法清空日志文件: {e.Message}");
			}
		}
		public static void Log(string message)
		{
			string timeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
			string logMessage = $"[{timeStamp}] {message}";

			try
			{
				// 如果是第一次写入，清空文件
				if (isFirstWrite)
				{
					File.WriteAllText(logPath, $"=== Unity UI 生成日志 ===\n{logMessage}\n");
					isFirstWrite = false;
				}
				else
				{
					File.AppendAllText(logPath, logMessage + "\n");
				}
			}
			catch (Exception e)
			{
				// 如果文件写入失败，改用控制台输出
				Console.WriteLine($"[日志系统错误] 无法写入日志文件: {e.Message}");
				Console.WriteLine(logMessage);
			}
		}

		public static void LogWarning(string message)
		{
			hadWarnning = true;
			LogWarningList.Add(message);
			Log($"[警告] {message}");
			Debug.LogWarning(message);
		}

		public static void LogError(string message)
		{
			Debug.LogError(message);
			throw new System.Exception(message); ;
		}
		public static void ShowErrorDialog(string title, string errMessage)
		{
			EditorUtility.DisplayDialog(title, errMessage, "确定");
		}

		private static string BuildExceptionDetail(Exception ex)
		{
			StringBuilder sb = new StringBuilder();
			int depth = 0;
			while (ex != null)
			{
				sb.AppendLine($"[Exception-{depth}] {ex.GetType().FullName}: {ex.Message}");
				if (!string.IsNullOrEmpty(ex.StackTrace))
				{
					sb.AppendLine("StackTrace:");
					sb.AppendLine(ex.StackTrace);
				}
				if (ex.Data != null && ex.Data.Count > 0)
				{
					sb.AppendLine("Data:");
					foreach (var key in ex.Data.Keys)
					{
						sb.AppendLine($"  {key}: {ex.Data[key]}");
					}
				}
				ex = ex.InnerException;
				depth++;
			}
			return sb.ToString();
		}

		public static void HandleAutoUIError(Exception err)
		{
			string detail = BuildExceptionDetail(err);
			// Unity 控制台输出完整信息
			Debug.LogError(detail);
			// 文件日志输出完整信息
			Log($"[错误] {detail}");
			// 弹窗给出简要提示，引导查看完整日志
			ShowErrorDialog("出现错误", "已生成完整错误日志，请通过 Tools/AutoUI - 打开日志文件 查看详情。");
			Hint();
		}

		// 最后程序结束时调用
		public static void Hint()
		{
			AutoUIGroupLayerProcessor.ClearExistPrefabNames();
			if (LogWarningList.Count > 0)
			{
				Log("=== 警告日志 ===");
				foreach (string warning in LogWarningList)
				{
					Log("警告汇总" + warning);
				}
			}
			if (hadWarnning)
			{
				EditorUtility.DisplayDialog("生成成功", "存在不严重的问题,请去桌面日志查看情况\n 提示: 请检索[警告]", "确定");
			}
			else
			{
				EditorUtility.DisplayDialog("提示", "运行成功", "确认");
			}
		}

		[MenuItem("Tools/AutoUI - 打开日志文件")]
		public static void OpenLogFile()
		{
			if (!File.Exists(logPath))
			{
				File.WriteAllText(logPath, "");
			}
			EditorUtility.RevealInFinder(logPath);
		}
	}
}


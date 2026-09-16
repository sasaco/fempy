using PDF_Manager;
using PdfSharpCore.Pdf;
using System;

using System.Text;
using System.Diagnostics;

public class Test_PDF_CLI
{
    public static void Main(String[] args)
    {
        // memo: Shift-JISを扱うためのおまじない
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        String data_dir = "../PDF_Test/TestData";
        String out_dir = ".";

        String[] files =
            Directory.GetFiles(data_dir, "*.json", SearchOption.TopDirectoryOnly)
            .Select(s => Path.GetFileName(s)).ToArray();

        for(int i=0; files.Length>i; i++)
        {
            Console.Write(i+1);
            Console.WriteLine(": " + files[i]);
        }

        Console.WriteLine();
        Console.Write("Select file(s): ");
        var user_input = Console.ReadLine();

        if(null == user_input)
        {
            Console.WriteLine("Bye");
            return;
        }

        if("" == user_input)
        {
            Console.WriteLine("Empty!");
            return;
        }

        String[] choices = user_input.Split(',');

        Console.WriteLine();
        Console.WriteLine("<< Your choice↓ >>");

        for(int i=0; choices.Length>i; i++)
        {
            int n = int.Parse(choices[i].Trim());

            if(!(0 < n && files.Length >= n))
                continue;

            Console.Write("Generating "); Console.Write(n); Console.Write(": ");
            Console.Write(files[n-1]); Console.Write("...");

            using (StreamReader st = new StreamReader(data_dir + "/" + files[n-1],
                                                      Encoding.GetEncoding("shift-jis")))
                      new PrintInput(st.ReadToEnd()).createPDF(out_dir + "/" + files[n-1] + ".pdf");

            Console.WriteLine("Done.");
        }

    }

    /*
    private static void show_pdf(string pdf_path)
    {
        var oProc = new Process();
        oProc.StartInfo.FileName = Path.GetFullPath(pdf_path);
        oProc.StartInfo.UseShellExecute = true;
        oProc.Start();
        oProc.WaitForExit();
    }
    */
}

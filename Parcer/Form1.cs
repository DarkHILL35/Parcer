using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AngleSharp.Html.Dom;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using AngleSharp.Html.Parser;
using AngleSharp.Dom;

namespace Parcer
{
    public partial class Form1 : Form
    {
        ParserWorker<string[]> parser_habr;
        public Form1()
        {
            InitializeComponent();
            parser_habr = new ParserWorker<string[]>(new HabrParser());
            //По заврешению работы парсера будет появляться уведомляющее окно.
            parser_habr.OnComplited += Parser_OnComplited;
            //Заполняем наш listBox заголовками
            parser_habr.OnNewData += Parser_OnNewData;
        }

        public void Parser_OnComplited(object o) { MessageBox.Show("Работа завершена!"); }
        public void Parser_OnNewData(object o, string[] str) { ListTitles.Items.AddRange(str); }

        private void buttonHabr_Click(object sender, EventArgs e)
        {
            parser_habr.Settings = new HabrSettings((int)numericUpDownStart.Value, (int)numericUpDownEnd.Value);
            //Парсим!
            parser_habr.Start();
        }

        private class HabrParser : IParser<string[]>
        {
            string[] IParser<string[]>.Parse(IHtmlDocument document)
            {
                throw new NotImplementedException();
            }
        }
    }
    interface IParserSettings
    {
        string BaseUrl { get; set; } //url сайта
        string Postfix { get; set; } //в постфикс будет передаваться id страницы
        int StartPoint { get; set; } //c какой страницы парсим данные
        int EndPoint { get; set; } //по какую страницу парсим данные
    }

    interface IParser<T> where T : class //класс реализующие этот интерфейс смогут возвращаться данные любого ссылочного типа
    {
        T Parse(IHtmlDocument document); // тип T при реализации будет заменяться на любой другой тип
    }

    class HtmlLoader
    {
        readonly HttpClient client; //для отправки HTTP запросов и получения HTTP ответов.
        readonly string url; //сюда будем передовать адрес.

        public HtmlLoader(IParserSettings settings)
        {
            client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "C# App"); //Это для индентификации на сайте-жертве.
            url = $"{settings.BaseUrl}/{settings.Postfix}/"; //Здесь собирается адресная строка
        }

        public async Task<string> GetSourceByPage(int id) // id - это id страницы
        {
            string currentUrl = url.Replace("{CurrentId}", id.ToString());//Подменяем {CurrentId} на номер страницы
            HttpResponseMessage responce = await client.GetAsync(currentUrl); //Получаем ответ с сайта.
            string source = default;

            if (responce != null && responce.StatusCode == HttpStatusCode.OK)
            {
                source = await responce.Content.ReadAsStringAsync(); //Помещаем код страницы в переменную.
            }
            return source;
        }
    }

    class ParserWorker<T> where T : class
    {
        IParser<T> parser;
        IParserSettings parserSettings; //настройки для загрузчика кода страниц
        HtmlLoader loader; //загрузчик кода страницы
        bool isActive; //активность парсера

        public IParser<T> Parser
        {
            get { return parser; }
            set { parser = value; }
        }

        public IParserSettings Settings
        {
            get { return parserSettings; }
            set
            {
                parserSettings = value; //Новые настройки парсера
                loader = new HtmlLoader(value); //сюда помещаются настройки для загрузчика кода страницы
            }
        }

        public bool IsActive //проверяем активность парсера.
        {
            get { return isActive; }
        }

        //Это событие возвращает спаршенные за итерацию данные( первый аргумент ссылка на парсер, и сами данные вторым аргументом)
        public event Action<object, T> OnNewData;
        //Это событие отвечает информирование при завершении работы парсера.
        public event Action<object> OnComplited;

        //1-й конструктор, в качестве аргумента будет передеваться класс реализующий интерфейс IParser
        public ParserWorker(IParser<T> parser)
        {
            this.parser = parser;
        }

        public void Start() //Запускаем парсер
        {
            isActive = true;
            Worker();
        }

        public void Stop() //Останавливаем парсер
        {
            isActive = false;
        }

        public async void Worker()
        {
            for (int i = parserSettings.StartPoint; i <= parserSettings.EndPoint; i++)
            {
                if (IsActive)
                {
                    string source = await loader.GetSourceByPage(i); //Получаем код страницы
                    //Здесь магия AngleShap, подробнее об интерфейсе IHtmlDocument и классе HtmlParser, 
                    //можно прочитать на GitHub, это интересное чтиво с примерами.
                    HtmlParser domParser = new HtmlParser();
                    IHtmlDocument document = await domParser.ParseDocumentAsync(source);
                    T result = parser.Parse(document);
                    OnNewData?.Invoke(this, result);
                }
            }

            OnComplited?.Invoke(this);
            isActive = false;
        }
    }

    class HabrSettings : IParserSettings
    {
        public HabrSettings(int start, int end)
        {
            StartPoint = start;
            EndPoint = end;
        }

        public string BaseUrl { get; set; } = "https://habr.ru"; //здесь прописываем url сайта.
        public string Prefix { get; set; } = "page{CurrentId}"; //вместо CurrentID будет подставляться номер страницы
        public int StartPoint { get; set; }
        public int EndPoint { get; set; }
        string IParserSettings.BaseUrl { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        string IParserSettings.Postfix { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        int IParserSettings.StartPoint { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        int IParserSettings.EndPoint { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    }

    public string[] Parse(IHtmlDocument document)
    {
        //Для хранения заголовков
        List<string> list = new List<string>();
        //Здесь мы получаем заголовки
        IEnumerable<IElement> items = document.QuerySelectorAll("a")
            .Where(item => item.ClassName != null && item.ClassName.Contains("post__title_link"));

        foreach (var item in items)
        {
            //Добавляем заголовки в коллекцию.
            list.Add(item.TextContent);
        }
        return list.ToArray();
    }
}

using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Runtime.InteropServices;

namespace RockAndStoneProjector;

/// <summary>
/// Проектор для создания модели по набору фотографий
/// </summary>
public class Projector
{
    /// <summary>
    /// Шаг в пикселях, также размер вокселя
    /// </summary>
    private int Step { get; set; }

    /// <summary>
    /// Шаг поворота объекта на изображениях в градусах
    /// </summary>
    private int AlphaStep { get; set; }

    /// <summary>
    /// Инициализация проектора
    /// </summary>
    /// <param name="step">Шаг в пикселях, также размер вокселя</param>
    /// <param name="alphaStep">Шаг поворота объекта на изображениях в градусах</param>
    public Projector(int step, int alphaStep)
    {
        Step = step;
        AlphaStep = alphaStep;
    }

    /// <summary>
    /// Возвращает лист всех файлов в директории по пути
    /// </summary>
    /// <param name="path">Путь к директории с файлами</param>
    /// <returns>Лист с адресами файлов</returns>
    private List<string> GetFiles(string path)
    {
        // Получаем лист с путями
        var files = new List<string>(Directory.GetFiles(path));

        // Сортируем лист
        files.Sort();

        // Возвращаем лист с путями
        return files;
    }

    /// <summary>
    /// Возвращает порядковый номер файла из его имени
    /// </summary>
    /// <param name="filename">Имя файла</param>
    /// <returns>Порядковый номер файла</returns>
    private double GetSerialNumber(string filename)
    {
        // Получим порядковый номер из имени файла
        var serial = Convert.ToDouble(
            filename.Substring(filename.LastIndexOf(".bmp", StringComparison.Ordinal) - 3, 3));

        // Вернём порядковый номер
        return serial;
    }

    /// <summary>
    /// Получить границы модели как массив горизонтальных слайсов с изображения
    /// </summary>
    /// <param name="pixelBuffer">Скопированное в буфер изображение</param>
    /// <param name="sourceData">Атрибуты изображения</param>
    /// <returns>Массив слайсов модели</returns>
    private List<Slice> GetForm(byte[] pixelBuffer, BitmapData sourceData)
    {
        // Массив для тестовых точек
        var form = new List<Slice>();

        // Граничное значение
        const int border = 90;

//         // Максимум по вертикали
//         var maxY = 750;
//
//         // Если максимум по вертикали больше высоты изображения, приравниваем его ей
//         if (maxY > sourceData.Height)
//              maxY = sourceData.Height;

        var maxY = sourceData.Height;

        // Получим левую границу модели

        // Проход по изображению по вертикали с шагом в пикселях
        for (var offsetY = 0; offsetY < maxY; offsetY += Step)
        {
            // Проход по горизонтали с шагом в пикселях по строке изображения
            for (var offsetX = 0; offsetX < sourceData.Width; offsetX += Step)
            {
                // Текущий индекс пикселя с учётом линейного байтового массива
                // Индекс строки умноженный на ширину строки плюс 
                // Индекс столбца умноженный на шаг
                var byteOffset = offsetY * sourceData.Stride + offsetX * Step;


                // Значение синего цвета пикселя 
                int b = pixelBuffer[byteOffset];

                // Значение зелёного цвета пикселя
                int g = pixelBuffer[byteOffset + 1];

                // Значение красного цвета пикселя
                int r = pixelBuffer[byteOffset + 2];

                // Если суммарно эти значения превышают некую границу, то пиксель считается частью модели
                if (r + g + b <= border)
                    continue;

                // Добавляем его в массив как горизонтальный слайс
                form.Add(new Slice(offsetY, offsetX, offsetX));
                // И идём на следующую строку
                break;
            }
        }

        // Минимум по вертикали - координата по Y первой точки в массиве
        var minY = form.First().Y;

        // Максимум по вертикали - координата по Y последней точки в массиве
        maxY = form.Last().Y;

        // Получим правую границу модели
        // Обход по вертикали от минимума до максимума с шагом
        for (var offsetY = minY; offsetY <= maxY; offsetY += Step)
        {
            //Обход по горизонтали справа-налево с шагом
            for (var offsetX = sourceData.Width - 1; offsetX > 0; offsetX -= Step)
            {
                // Текущий индекс пикселя
                var byteOffset = offsetY * sourceData.Stride + offsetX * Step;


                // Значения цветов пикселя
                int b = pixelBuffer[byteOffset];
                int g = pixelBuffer[byteOffset + 1];
                int r = pixelBuffer[byteOffset + 2];

                // Если значения больше границы, то считаем пиксель частью модели
                if (r + g + b <= border)
                    continue;

                // Вертикальный индекс пикселя
                var index = (offsetY - minY) / Step;

                // Если этот индекс включен в массив
                if (index < form.Count)
                    // Записываем в правую горизонтальную координату координату правого пикселя
                    form[index].Xr = offsetX;
                // И идём на следующую строку
                break;
            }
        }

        // Возвращаем массив с границами модели
        return form;
    }


    /// <summary>
    /// Тут творится хрень
    /// </summary>
    /// <param name="sourceBitmap">Изображение с одного ракурса</param>
    /// <returns>Массив слайсов модели</returns>
    private List<Slice> GetFormButWeirdly(Bitmap sourceBitmap)
    {
        // Блокируем битмап в системной памяти только для чтения
        var sourceData = sourceBitmap.LockBits(
            new Rectangle(0, 0, sourceBitmap.Width, sourceBitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);


        // Буферный массив размером в ширину шага (одной строки пикселей с округлением до четырёхбайтовой границы),
        // умноженной на высоту
        var pixelBuffer = new byte[sourceData.Stride * sourceData.Height];

        // Копируем изображение в буфер
        Marshal.Copy(sourceData.Scan0, pixelBuffer, 0, pixelBuffer.Length);


        // Получаем массив слайсов с модели
        var points = GetForm(pixelBuffer, sourceData);

        // Копируем результат обратно по адресу изображения
        Marshal.Copy(pixelBuffer, 0, sourceData.Scan0, pixelBuffer.Length);

        // Разблокируем битмап, который блокировали ранее
        sourceBitmap.UnlockBits(sourceData);

        // Возвращаем массив слайсов с модели, который мы получили из метода, который возвращает массив слайсов с модели
        return points;
    }

    /// <summary>
    /// Нормализует модель, эффективно сдвигая её в левый верхний угол по координатам
    /// </summary>
    /// <param name="testPoints">Ссылка на массив горизонтальных слайсов</param>
    private void NormalizeForm(ref List<Slice> testPoints)
    {
        // Минимум по вертикали - координата первой записи в массиве
        var minY = testPoints.First().Y;

        // Минимум по горизонтали - наименьшая координата 
        var minX = testPoints.Min(p => p.Xl);

        // Вычитаем из всех координат слайсов минимальное значение, чтобы "сдвинуть" модель
        foreach (var p in testPoints)
        {
            p.Y -= minY;
            p.Xl -= minX;
            p.Xr -= minX;
        }
    }

    /// <summary>
    /// Создать прямоугольное облако точек по размерам модели
    /// </summary>
    /// <param name="ySize">Размер модели по вертикали</param>
    /// <param name="minSize">Левая граница модели</param>
    /// <param name="maxSize">Правая граница модели</param>
    /// <returns>Лист трёхмерных точек</returns>
    private List<Point3D> GetPointCloudRectangle(int ySize, int minSize, int maxSize)
    {
        // Лист для хранения облака точек
        var pointCloud = new List<Point3D>();

        // Заполним облако точек точками
        // Обход с шагом по всем трём максимальным координатам модели
        for (var y = 0; y < ySize; y += Step)
        {
            for (var x = 0; x < minSize; x += Step)
            {
                for (var z = 0; z < maxSize; z += Step)
                {
                    // Добавление в лист точки
                    pointCloud.Add(new Point3D(x, y, z));
                }
            }
        }

        // Вернём облако точек
        return pointCloud;
    }

    /// <summary>
    /// Первый вырез формы из облака точек
    /// </summary>
    /// <param name="pointCloud">Ссылка на облако точек</param>
    /// <param name="form">Массив горизонтальных слайсов</param>
    private void MakeFirstCut(ref List<Point3D> pointCloud, List<Slice> form)
    {
        // Создадим новое пустое облако точек
        var newPointCloud = new List<Point3D>(pointCloud.Count);

        // Проход по всем точкам в облаке
        foreach (var point in pointCloud)
        {
            // Проход по всем горизонтальным слайсам
            foreach (var slice in form)
            {
                // Если точка в облаке попадает под горизонтальный слайс на одной Y координате
                if (point.X <= slice.Xl ||
                    point.X >= slice.Xr ||
                    point.Y != slice.Y)
                    continue;
                // Добавим её в новое облако точек
                newPointCloud.Add(point);
                // Переходим на следующий слайс
                break;
            }
        }

        // Очищаем предыдущее облако точек
        pointCloud.Clear();

        // И перезаписываем в него 
        pointCloud = newPointCloud;
    }


    /// <summary>
    /// Формула координаты Х проекции точки
    /// </summary>
    /// <param name="pointCloud">Облако точек</param>
    /// <param name="currentAlphaStep">Текущий угол поворота формы</param>
    /// <param name="j">Индекс точки</param>
    /// <returns>Координата Х проекции точки</returns>
    private int GetPointProjection(List<Point3D> pointCloud, int currentAlphaStep, int j)
    {
        var beta = Math.Atan2(pointCloud[j].Z, pointCloud[j].X);
        return (int)(Math.Sqrt(pointCloud[j].X * pointCloud[j].X + pointCloud[j].Z * pointCloud[j].Z) *
                     Math.Cos(beta + currentAlphaStep * Math.PI / 180.0));
    }

    /// <summary>
    /// Последующие вырезы формы из облака точек
    /// </summary>
    /// <param name="pointCloud">Облако точек</param>
    /// <param name="form">Форма</param>
    /// <param name="currentAlphaStep">Текущий угол поворота формы</param>
    private void MakeSecondCut(ref List<Point3D> pointCloud, List<Slice> form, int currentAlphaStep)
    {
        // Пустое облако точек
        var newPointCloud = new List<Point3D>(pointCloud.Count);

        // Пустая проекция
        var projection = new List<Slice>();

        // Заполнение проекции минимальным и максимальным значениями типа данных ???
        for (var i = 0; i < form.Count; i++)
        {
            projection.Add(new Slice(i, int.MaxValue, int.MinValue));
        }

        // Проход по всем точкам облака точек
        for (var j = 0; j < pointCloud.Count; j++)
        {
            // Получение координаты Х проекции точки
            var x = GetPointProjection(pointCloud, currentAlphaStep, j);

            // Индекс точки - её Y координата, делённая на шаг
            var index = pointCloud[j].Y / Step;

            // Пропускаем точку, если её индекс по вертикали больше, чем размер проекции
            if (index >= projection.Count)
                continue;

            // Перенос координат Х
            if (projection[index].Xl > x)
                projection[index].Xl = x;

            if (projection[index].Xr < x)
                projection[index].Xr = x;
        }

        // Наибольшая правая координата и наименьшая левая координата, делённые на 2, для проекции
        var maxPrX1 = (projection.Max(line => line.Xr) + projection.Min(line => line.Xl)) / 2;

        // Наибольшая правая и наименьшая левая координаты, делённые на 2, для секции
        var maxSecX1 = (form.Max(line => line.Xr) + form.Min(line => line.Xl)) / 2;

        // Разница между ними
        var diff = maxPrX1 - maxSecX1;


        // Обход по всем точкам облака
        for (var j = 0; j < pointCloud.Count; j++)
        {
            // Получение Х координаты проекции точки, но с учётом разницы для выравнивания границ
            var x = GetPointProjection(pointCloud, currentAlphaStep, j) - diff;

            // Индекс по вертикали (координата Y, делённая на шаг)
            var i = pointCloud[j].Y / Step;

            // Условие принадлежности точки форме
            if (i < form.Count &&
                x > form[i].Xl &&
                x < form[i].Xr &&
                pointCloud[j].Y == form[i].Y)
                // Запись точки в пустое облако
                newPointCloud.Add(pointCloud[j]);
        }

        // Перезаписывание нового облака поверх старого
        pointCloud = newPointCloud;
    }

    /// <summary>
    /// Удаление всех точек внутри формы
    /// </summary>
    /// <param name="pointCloud">Ссылка на облако точек</param>
    private void RemoveInside(ref List<Point3D> pointCloud)
    {
        // Пустое облако точек
        var newPointCloud = new List<Point3D>();

        // Инициализация границ
        int minX = int.MaxValue,
            minY = int.MaxValue,
            minZ = int.MaxValue,
            maxX = int.MinValue,
            maxY = int.MinValue,
            maxZ = int.MinValue;

        // Нахождение минимальных и максимальных значений для каждой оси
        foreach (var point in pointCloud)
        {
            if (point.X < minX)
                minX = point.X;

            if (point.X > maxX)
                maxX = point.X;

            if (point.Y < minY)
                minY = point.Y;

            if (point.Y > maxY)
                maxY = point.Y;

            if (point.Z < minZ)
                minZ = point.Z;

            if (point.Z > maxZ)
                maxZ = point.Z;
        }

        // Словарь для линий
        var lines = new Dictionary<int, Dictionary<int, List<int>>>();

        // Обход по облаку точек для x линий
        foreach (var p in pointCloud)
        {
            // Странная хрень
            // Перевод облака точек в новый формат линий?
            var z = p.Z;
            var y = p.Y;

            if (!lines.Keys.Contains(z))
            {
                lines[z] = new Dictionary<int, List<int>>();
            }

            if (lines[z].Keys.Contains(y))
                lines[z][y].Add(p.X);
            else
                lines[z][y] = new List<int>(new[] { p.X });
        }

        // Для каждой линии берутся крайняя левая и крайняя правая точки
        foreach (var z in lines.Keys)
        {
            foreach (var y in lines[z].Keys)
            {
                var x0 = lines[z][y].Min();
                var x1 = lines[z][y].Max();
                newPointCloud.Add(new Point3D(x0, y, z));
                newPointCloud.Add(new Point3D(x1, y, z));
            }
        }

        // Обход по облаку точек для z линий
        lines = new Dictionary<int, Dictionary<int, List<int>>>();

        foreach (var p in pointCloud)
        {
            var x = p.X;
            var y = p.Y;

            if (!lines.Keys.Contains(x))
            {
                lines[x] = new Dictionary<int, List<int>>();
            }

            if (lines[x].Keys.Contains(y))
                lines[x][y].Add(p.Z);
            else
                lines[x][y] = new List<int>(new[] { p.Z });
        }

        foreach (var x in lines.Keys)
        {
            foreach (var y in lines[x].Keys)
            {
                var z0 = lines[x][y].Min();
                var z1 = lines[x][y].Max();
                newPointCloud.Add(new Point3D(x, y, z0));
                newPointCloud.Add(new Point3D(x, y, z1));
            }
        }

        // Обход по облаку точек для y линий
        lines = new Dictionary<int, Dictionary<int, List<int>>>();

        foreach (var p in pointCloud)
        {
            var x = p.X;
            var z = p.Z;

            if (!lines.Keys.Contains(z))
            {
                lines[z] = new Dictionary<int, List<int>>();
            }

            if (lines[z].Keys.Contains(x))
                lines[z][x].Add(p.Y);
            else
                lines[z][x] = new List<int>(new[] { p.Y });
        }

        foreach (var z in lines.Keys)
        {
            foreach (var x in lines[z].Keys)
            {
                var y0 = lines[z][x].Min();
                var y1 = lines[z][x].Max();
                newPointCloud.Add(new Point3D(x, y0, z));
                newPointCloud.Add(new Point3D(x, y1, z));
            }
        }

        // Очистка переданного по ссылке облака точек
        pointCloud.Clear();

        // Копирование результата в облако точек
        pointCloud = newPointCloud;
    }


    /// <summary>
    /// Сжатие облака точек
    /// </summary>
    /// <param name="pointCloud">Облако точек по ссылке</param>
    private void ShrinkVirtualObject(ref List<Point3D> pointCloud)
    {
        // Коэффициент для сжатия модели
        var shrinkValue = Step / 2;

        // if (Step > 1)
        // {
        //     halfStep = Step / 2;
        //     if (Step % 2 > 0)
        //         halfStep++;
        // }


        // Деление всех целочисленных координат модели на определённое число
        for (var i = 0; i < pointCloud.Count; i++)
        {
            pointCloud[i] = new Point3D(
                pointCloud[i].X / shrinkValue,
                pointCloud[i].Y / shrinkValue,
                pointCloud[i].Z / shrinkValue);
        }
    }

    /// <summary>
    /// Набор полигонов для отрисовки куба
    /// </summary>
    private static readonly int[][][] StlCoords =
    {
        new[] { new[] { -1, -1, -1 }, new[] { 1, 1, -1 }, new[] { 1, -1, -1 } },
        new[] { new[] { 1, 1, 1 }, new[] { 1, -1, -1 }, new[] { 1, 1, -1 } },
        new[] { new[] { 1, 1, 1 }, new[] { 1, 1, -1 }, new[] { -1, 1, 1 } },
        new[] { new[] { 1, -1, 1 }, new[] { 1, -1, -1 }, new[] { 1, 1, 1 } },
        new[] { new[] { 1, -1, 1 }, new[] { 1, 1, 1 }, new[] { -1, 1, 1 } },
        new[] { new[] { 1, -1, 1 }, new[] { -1, -1, -1 }, new[] { 1, -1, -1 } },
        new[] { new[] { -1, -1, 1 }, new[] { -1, 1, 1 }, new[] { -1, -1, -1 } },
        new[] { new[] { -1, -1, 1 }, new[] { -1, -1, -1 }, new[] { 1, -1, 1 } },
        new[] { new[] { -1, -1, 1 }, new[] { 1, -1, 1 }, new[] { -1, 1, 1 } },
        new[] { new[] { -1, 1, -1 }, new[] { -1, -1, -1 }, new[] { -1, 1, 1 } },
        new[] { new[] { -1, 1, -1 }, new[] { -1, 1, 1 }, new[] { 1, 1, -1 } },
        new[] { new[] { -1, 1, -1 }, new[] { 1, 1, -1 }, new[] { -1, -1, -1 } }
    };

    /// <summary>
    /// Создание записи о полигоне по трём точкам
    /// </summary>
    /// <param name="facet">Форма полигона грани</param>
    /// <param name="point">Точка из облака точек</param>
    /// <returns>Строка с гранью</returns>
    private string GetFacet(int[][] facet, Point3D point)
    {
        // Инициализация строки
        var outStr = "\tfacet normal 0 0 0\n\t\touter loop\n";

        // Обход по точкам полигона
        foreach (var p in facet)
        {
            // Добавление записей о точках
            var vertex = string.Format("\t\t\tvertex {0} {1} {2}\n",
                point.X + p[0],
                point.Y + p[1],
                point.Z + p[2]);

            outStr += vertex;
        }

        // Завершающая строка грани
        outStr += "\t\tendloop\n\tendfacet\n";

        // Возврат результата
        return outStr;
    }

    /// <summary>
    /// Получение информации о вокселе в текстовом формате
    /// </summary>
    /// <param name="point">Точка из облака точек</param>
    /// <returns>Строку с данными о вокселе</returns>
    private string GetVoxel(Point3D point)
    {
        // Инициализация строки
        var outStr = new StringBuilder("");

        // Присоединение граней вокселя как полигонов
        for (var i = 0; i < 12; i++)
            outStr.Append(GetFacet(StlCoords[i], point));

        // Возврат результата
        return outStr.ToString();
    }

    /// <summary>
    /// Сохранение облака точек в формате STL
    /// </summary>
    /// <param name="points">Облако точек</param>
    /// <param name="path">Путь</param>
    private void SaveStl(List<Point3D> points, string path)
    {
        // Инициализируем строку
        var outStr = new StringBuilder("solid model\n");

        // Присоединяем точки из облака как воксели
        foreach (var point in points)
        {
            outStr.Append(GetVoxel(point));
        }

        // Завершающая строка STL файла
        outStr.Append("endsolid model");

        // Сохраним по пути в формате .stl
        File.WriteAllText(path + ".stl", outStr.ToString());
    }


    /// <summary>
    /// Сохранение облака точек в ASCII формате XYZ
    /// </summary>
    /// <param name="points">Облако точек</param>
    /// <param name="path">Путь</param>
    private void SaveXyz(List<Point3D> points, string path)
    {
        // Инициализируем строку
        var outStr = new StringBuilder("");

        // Присоединяем точки из облака как воксели
        foreach (var point in points)
        {
            outStr.Append(string.Format("{0} {1} {2}\n",
                point.X,
                point.Y,
                point.Z));
        }

        // Сохраним по пути в формате .xyz
        File.WriteAllText(path + ".xyz", outStr.ToString());
    }


    public void GenerateModel(string path)
    {
        // Пустой лист для форм
        var forms = new List<List<Slice>>();

        foreach (var filepath in GetFiles(path))
        {
            // Имя файла изображения
            var filename = filepath.Substring(path.Length + 1);

            // Пропуск если файл не существует или не изображение
            // TODO: нужно запилить обработку файлов разных форматов
            if (!File.Exists(filepath) || !filepath.EndsWith(".bmp"))
                continue;

            // Порядковый номер файла
            var serialNumber = GetSerialNumber(filepath);

            // Изображение в битмап
            var bitmapImage = (Bitmap)Image.FromFile(filepath);

            // Получим массив горизонтальных слайсов с изображения
            var form = GetFormButWeirdly(bitmapImage);

            // Нормализуем массив слайсов
            NormalizeForm(ref form);

            // Добавим его как секцию
            forms.Add(form);

            //Выведем в консоль информацию о конкретной секции
            // Имя файла
            // Количество горизонтальных слайсов
            // Порядковый номер изображения
            // Наибольшая разница между горизонтальными координатами (ширина модели)

            // что-то странное, надо понять, что это вообще
            // Наименьший слайс, в котором левая и правая координаты равны
            Console.WriteLine("{0}: {1}, alpha: {2}, max size: {3}, min size: {4}",
                filename,
                form.Count,
                serialNumber,
                form.Max(point => point.Xr - point.Xl),
                form.Min(point => point.Xr = point.Xl));
        }

        // Лист с размерами
        var sizes = new List<int>();

        // Обход по всем секциям
        foreach (var form in forms)
        {
            // аналогично, что за максимальный слайс в секции, у которого равны координаты?
            // Запись в размер наибольшего элемента, у которого равны горизонтальные координаты
            sizes.Add(form.Max(line => line.Xr = line.Xl));
        }

        // Наименьший размер модели по горизонтали и его индекс
        var minSize = sizes.Min();
        var minSecIndex = sizes.IndexOf(minSize);
        Console.WriteLine("Min size: {0}; min size index {1}, alpha: {2}",
            minSize,
            minSecIndex,
            minSecIndex * AlphaStep);

        // Наибольший размер по горизонтали и его индекс
        var maxSize = sizes.Max();
        var maxSecIndex = sizes.IndexOf(maxSize);
        Console.WriteLine("Max size: {0}; max size index {1}, alpha: {2}",
            maxSize,
            maxSecIndex,
            maxSecIndex * AlphaStep);


        // Размер по вертикали
        var ySize = forms.Max(form => form.Last().Y - form.First().Y);


        // Создание облака точек в форме куба в размер
        Console.WriteLine("Make Virtual Object");
        var virtualObject = GetPointCloudRectangle(ySize, minSize, maxSize);

        // Первый вырез проекции в облако точек
        Console.WriteLine("Make Cuts");
        MakeFirstCut(ref virtualObject, forms[minSecIndex]);


        // Проход по углам с шагом
        for (var i = 0; i < (100 / AlphaStep); i++)
        {
            var j = i + minSecIndex;
            if (j >= forms.Count)
                j -= forms.Count;

            // Последующие вырезания проекций из облака точек
            MakeSecondCut(ref virtualObject, forms[j], AlphaStep * i);
            Console.WriteLine("Size: {0}", virtualObject.Count);
        }

        // Удаление внутренних точек из облака
        Console.WriteLine("Remove Inside");
        RemoveInside(ref virtualObject);

        // Сжатие облака точек
        ShrinkVirtualObject(ref virtualObject);

        // Сохранение в формате XYZ
        Console.WriteLine("Save XYZ");
        SaveXyz(virtualObject, path + "\\points");

        // Вокселизация и сохранение в формате STL
        Console.WriteLine("Save STL");
        SaveStl(virtualObject, path + "\\points");
    }
}
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Runtime.InteropServices;

namespace RockAndStoneProjector;

/// <summary>
/// Проектор для создание модели по набору фотографий
/// </summary>
public static class Projector
{
    /// <summary>
    /// Путь к директории с изображениями
    /// </summary>
    private const string Path = @"C:\Images\tall_one";

    /// <summary>
    /// Получить лист всех файлов в директории по пути
    /// </summary>
    /// <param name="path">Адрес директории</param>
    /// <returns>Лист с адресами файлов</returns>
    private static List<string> GetFiles(string path)
    {
        // Получаем лист с путями
        var files = new List<string>(Directory.GetFiles(path));

        // Сортируем лист
        files.Sort();

        // Возвращаем лист с путями
        return files;
    }

    /// <summary>
    /// Получить порядковый номер файла из его имени
    /// </summary>
    /// <param name="file">Имя файла</param>
    /// <returns>Порядковый номер файла</returns>
    private static double GetAlpha(string file)
    {
        // Извлечём порядковый номер как строку
        var alphaStr = file.Substring(file.LastIndexOf(".bmp", StringComparison.Ordinal) - 3, 3);

        // Переведём его в числовой формат
        var alpha = Convert.ToDouble(alphaStr);

        // Вернём порядковый номер
        return alpha;
    }

    /// <summary>
    /// Получить границы модели как массив горизонтальных слайсов с изображения
    /// </summary>
    /// <param name="pixelBuffer">Скопированное в буфер изображение</param>
    /// <param name="sourceData">Атрибуты изображения</param>
    /// <param name="step">Шаг прохода по изображению в пикселях</param>
    /// <returns>Массив слайсов модели</returns>
    private static List<DoublePoint> GetTestPoints(byte[] pixelBuffer, BitmapData sourceData, int step)
    {
        // Массив для тестовых точек
        var testPoints = new List<DoublePoint>();

        // Граничное значение
        const int border = 90;

        // Максимум по вертикали
        var maxY = 750;

        // Если максимум по вертикали больше высоты изображения, приравниваем его ей
#pragma warning disable CA1416
        if (maxY > sourceData.Height)
            maxY = sourceData.Height;

        // Получим левую границу модели

        // Проход по изображению по вертикали с шагом в пикселях
        for (var offsetY = 0; offsetY < maxY; offsetY += step)
        {
            // Проход по горизонтали с шагом в пикселях по строке изображения
            for (var offsetX = 0; offsetX < sourceData.Width; offsetX += step)
            {
                // Текущий индекс пикселя с учётом линейного байтового массива
                // Индекс строки умноженный на ширину строки плюс 
                // Индекс столбца умноженный на шаг
                var byteOffset = offsetY * sourceData.Stride + offsetX * step;


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
                testPoints.Add(new DoublePoint(offsetY, offsetX, offsetX));
                // И идём на следующую строку
                break;
            }
        }
#pragma warning restore CA1416

        // Минимум по вертикали - координата по Y первой точки в массиве
        var minY = testPoints.First().Y;

        // Максимум по вертикали - координата по Y последней точки в массиве
        maxY = testPoints.Last().Y;

        // Получим правую границу модели
#pragma warning disable CA1416
        // Обход по вертикали от минимума до максимума с шагом
        for (var offsetY = minY; offsetY <= maxY; offsetY += step)
        {
            //Обход по горизонтали справа-налево с шагом
            for (var offsetX = sourceData.Width - 1; offsetX > 0; offsetX -= step)
            {
                // Текущий индекс пикселя

                var byteOffset = offsetY * sourceData.Stride + offsetX * step;


                // Значения цветов пикселя
                int b = pixelBuffer[byteOffset];
                int g = pixelBuffer[byteOffset + 1];
                int r = pixelBuffer[byteOffset + 2];

                // Если значения больше границы, то считаем пиксель частью модели
                if (r + g + b <= border)
                    continue;

                // Вертикальный индекс пикселя
                var index = (offsetY - minY) / step;

                // Если этот индекс включен в массив
                if (index < testPoints.Count)
                    // Записываем в правую горизонтальную координату координату правого пикселя
                    testPoints[index].X1 = offsetX;
                // И идём на следующую строку
                break;
            }
        }
#pragma warning restore CA1416

        // Возвращаем массив с границами модели
        return testPoints;
    }


    /// <summary>
    /// </summary>
    /// <param name="sourceBitmap">Изображение с одного ракурса</param>
    /// <param name="step">Шаг по изображению в пикселях</param>
    /// <returns>Массив слайсов модели</returns>
    private static List<DoublePoint> GetObjectDoublePoints(Bitmap sourceBitmap, int step)
    {
        // Блокируем битмап в системной памяти только для чтения
#pragma warning disable CA1416
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
        var points = GetTestPoints(pixelBuffer, sourceData, step);

        // Копируем результат обратно по адресу изображения
        Marshal.Copy(pixelBuffer, 0, sourceData.Scan0, pixelBuffer.Length);

        // Разблокируем битмап, который блокировали ранее
        sourceBitmap.UnlockBits(sourceData);
#pragma warning restore CA1416

        // Возвращаем массив слайсов с модели, который мы получили из метода, который возвращает массив слайсов с модели
        return points;
    }

    /// <summary>
    /// Нормализует модель, эффективно сдвигая её в левый верхний угол по координатам
    /// </summary>
    /// <param name="testPoints">Ссылка на массив горизонтальных слайсов</param>
    private static void NormalizeTestPoints(ref List<DoublePoint> testPoints)
    {
        // Минимум по вертикали - координата первой записи в массиве
        var minY = testPoints.First().Y;

        // Минимум по горизонтали - наименьшая координата 
        var minX = testPoints.Min(p => p.X0);

        // Вычитаем из всех координат слайсов минимальное значение, чтобы "сдвинуть" модель
        foreach (var p in testPoints)
        {
            p.Y -= minY;
            p.X0 -= minX;
            p.X1 -= minX;
        }
    }

    /// <summary>
    /// Создать прямоугольное облако точек по размерам модели
    /// </summary>
    /// <param name="step">Шаг в пикселях</param>
    /// <param name="ySize">Размер модели по вертикали</param>
    /// <param name="minSize">Левая граница модели</param>
    /// <param name="maxSize">Правая граница модели</param>
    /// <returns>Лист трёхмерных точек</returns>
    private static List<Point3D> MakeVirtualObject(int step, int ySize, int minSize, int maxSize)
    {
        // Лист для хранения облака точек
        var virtualObject = new List<Point3D>();

        // Заполним облако точек точками
        // Обход с шагом по всем трём максимальным координатам модели
        for (var y = 0; y < ySize; y += step)
        {
            for (var x = 0; x < minSize; x += step)
            {
                for (var z = 0; z < maxSize; z += step)
                {
                    // Добавление в лист точки
                    virtualObject.Add(new Point3D(x, y, z));
                }
            }
        }

        // Вернём облако точек
        return virtualObject;
    }

    /// <summary>
    /// Первый вырез формы из облака точек
    /// </summary>
    /// <param name="virtualObject">Ссылка на облако точек</param>
    /// <param name="section">Массив горизонтальных слайсов</param>
    private static void MakeFirstCut(ref List<Point3D> virtualObject, List<DoublePoint> section)
    {
        // Создадим новое пустое облако точек
        var newVirtualObject = new List<Point3D>(virtualObject.Count);

        // Проход по всем точкам в облаке
        foreach (var p in virtualObject)
        {
            // Проход по всем горизонтальным слайсам
            foreach (var s in section)
            {
                // Если точка в облаке попадает под горизонтальный слайс на одной Y координате
                if (p.X <= s.X0 ||
                    p.X >= s.X1 ||
                    p.Y != s.Y)
                    continue;
                // Добавим её в новое облако точек
                newVirtualObject.Add(p);
                // Переходим на следующий слайс
                break;
            }
        }

        // Очищаем предыдущее облако точек
        virtualObject.Clear();

        // И перезаписываем в него 
        virtualObject = newVirtualObject;
    }


    /// <summary>
    /// Формула координаты Х проекции точки
    /// </summary>
    /// <param name="virtualObject">Облако точек</param>
    /// <param name="alphaStepRadians">Шаг угла в радианах</param>
    /// <param name="j">Индекс точки</param>
    /// <returns>Координата Х проекции точки</returns>
    private static int GetPointProjection(List<Point3D> virtualObject, double alphaStepRadians, int j)
    {
        var beta = Math.Atan2(virtualObject[j].Z, virtualObject[j].X);
        return (int)(Math.Sqrt(virtualObject[j].X * virtualObject[j].X + virtualObject[j].Z * virtualObject[j].Z) *
                     Math.Cos(beta + alphaStepRadians));
    }

    /// <summary>
    /// Последующие вырезы формы из облака точек
    /// </summary>
    /// <param name="virtualObject">Облако точек</param>
    /// <param name="section">Форма</param>
    /// <param name="step">Шаг в пикселях</param>
    /// <param name="alphaStep">Шаг угловой</param>
    /// <param name="delta">????????????</param>
    private static void MakeSecondCut(ref List<Point3D> virtualObject, List<DoublePoint> section, int step,
        int alphaStep, int delta)
    {
        // Пустое облако точек
        var newVirtualObject = new List<Point3D>(virtualObject.Count);

        // Угловой шаг в радианах
        var alphaStepRadians = alphaStep * Math.PI / 180.0;

        // Пустая проекция
        var projection = new List<DoublePoint>();

        // Заполнение проекции минимальным и максимальным значениями типа данных ???
        for (var i = 0; i < section.Count; i++)
        {
            projection.Add(new DoublePoint(i, int.MaxValue, int.MinValue));
        }

        // Проход по всем точкам облака точек
        for (var j = 0; j < virtualObject.Count; j++)
        {
            // Получение координаты Х проекции точки
            var x = GetPointProjection(virtualObject, alphaStepRadians, j);

            // Индекс точки - её Y координата, делённая на шаг
            var index = virtualObject[j].Y / step;

            // Пропускаем точку, если её индекс по вертикали больше, чем размер проекции
            if (index >= projection.Count)
                continue;

            // Перенос координат Х
            if (projection[index].X0 > x)
                projection[index].X0 = x;

            if (projection[index].X1 < x)
                projection[index].X1 = x;
        }

        // Наибольшая правая координата и наименьшая левая координата, делённые на 2, для проекции
        var maxPrX1 = (projection.Max(line => line.X1) + projection.Min(line => line.X0)) / 2;

        // Наибольшая правая и наименьшая левая координаты, делённые на 2, для секции
        var maxSecX1 = (section.Max(line => line.X1) + section.Min(line => line.X0)) / 2;

        // Разница между ними
        var diff = maxPrX1 - maxSecX1;


        // Обход по всем точкам облака
        for (var j = 0; j < virtualObject.Count; j++)
        {
            // Получение Х координаты проекции точки, но с учётом разницы для выравнивания границ
            var x = GetPointProjection(virtualObject, alphaStepRadians, j) - diff;

            // Индекс по вертикали (координата Y, делённая на шаг)
            var i = virtualObject[j].Y / step;

            // Условие принадлежности точки форме
            if (i < section.Count &&
                x > section[i].X0 - delta &&
                x < section[i].X1 + delta &&
                virtualObject[j].Y == section[i].Y)
                // Запись точки в пустое облако
                newVirtualObject.Add(virtualObject[j]);
        }

        // Перезаписывание нового облака поверх старого
        virtualObject = newVirtualObject;
    }

    /// <summary>
    /// Удаление всех точек внутри формы
    /// </summary>
    /// <param name="virtualObject">Ссылка на облако точек</param>
    private static void RemoveInside(ref List<Point3D> virtualObject)
    {
        // Пустое облако точек
        var newVirtualObject = new List<Point3D>();

        // Инициализация границ
        int minX = int.MaxValue,
            minY = int.MaxValue,
            minZ = int.MaxValue,
            maxX = int.MinValue,
            maxY = int.MinValue,
            maxZ = int.MinValue;

        // Нахождение минимальных и максимальных значений для каждой оси
        foreach (var p in virtualObject)
        {
            if (p.X < minX)
                minX = p.X;

            if (p.X > maxX)
                maxX = p.X;

            if (p.Y < minY)
                minY = p.Y;

            if (p.Y > maxY)
                maxY = p.Y;

            if (p.Z < minZ)
                minZ = p.Z;

            if (p.Z > maxZ)
                maxZ = p.Z;
        }

        // Словарь для линий
        var lines = new Dictionary<int, Dictionary<int, List<int>>>();

        // Обход по облаку точек для x линий
        foreach (var p in virtualObject)
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
                newVirtualObject.Add(new Point3D(x0, y, z));
                newVirtualObject.Add(new Point3D(x1, y, z));
            }
        }

        // Обход по облаку точек для z линий
        lines = new Dictionary<int, Dictionary<int, List<int>>>();

        foreach (var p in virtualObject)
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
                newVirtualObject.Add(new Point3D(x, y, z0));
                newVirtualObject.Add(new Point3D(x, y, z1));
            }
        }

        // Обход по облаку точек для y линий
        lines = new Dictionary<int, Dictionary<int, List<int>>>();

        foreach (var p in virtualObject)
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
                newVirtualObject.Add(new Point3D(x, y0, z));
                newVirtualObject.Add(new Point3D(x, y1, z));
            }
        }

        // Очистка переданного по ссылке облака точек
        virtualObject.Clear();

        // Копирование результата в облако точек
        virtualObject = newVirtualObject;
    }


    /// <summary>
    /// Сжатие облака точек
    /// </summary>
    /// <param name="virtualObject">Облако точек по ссылке</param>
    /// <param name="step">Шаг в пикселях</param>
    private static void ShrinkVirtualObject(ref List<Point3D> virtualObject, int step)
    {
        // Коэффициент для сжатия модели
        var halfStep = 1;

        if (step > 1)
        {
            halfStep = step / 2;
            if (step % 2 > 0)
                halfStep++;
        }


        // Деление всех целочисленных координат модели на определённое число
        for (var i = 0; i < virtualObject.Count; i++)
        {
            virtualObject[i] = new Point3D(
                virtualObject[i].X / halfStep,
                virtualObject[i].Y / halfStep,
                virtualObject[i].Z / halfStep);
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
    /// <param name="arr">Форма полигона грани</param>
    /// <param name="point">Точка из облака точек</param>
    /// <returns>Строка с гранью</returns>
    private static string GetFacet(int[][] arr, Point3D point)
    {
        // Инициализация строки
        var outStr = "\tfacet normal 0 0 0\n\t\touter loop\n";

        // Обход по точкам полигона
        foreach (var p in arr)
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
    private static string GetVoxel(Point3D point)
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
    private static void SaveStl(List<Point3D> points, string path)
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
    private static void SaveXyz(List<Point3D> points, string path)
    {
        // Инициализируем строку
        var outStr = new StringBuilder("");

        // Присоединяем точки из облака как воксели
        foreach (var point in points)
        {
            outStr.Append(string.Format("{0} {1} {2}",
                point.X,
                point.Y,
                point.Z));
        }

        // Сохраним по пути в формате .xyz
        File.WriteAllText(path + ".xyz", outStr.ToString());
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="path">Путь к изображениям</param>
    /// <param name="step">Шаг в пикселях</param>
    /// <param name="alphastep">Шаг угла поворота объекта</param>
    public static void GenerateModel(string path, int step, int alphastep)
    {
        // Пустой лист для секций - массивов горизонтальных слайсов модели
        var sections = new List<List<DoublePoint>>();

        foreach (var file in GetFiles(path))
        {
            // Имя файла изображения
            var shortName = file.Substring(path.Length + 1);

            if (File.Exists(file) && file.EndsWith(".bmp"))
            {
                // Порядковый номер файла
                var alpha = GetAlpha(file);

                // Изображение в битмап
#pragma warning disable CA1416
                var bmp = (Bitmap)Image.FromFile(file);
#pragma warning restore CA1416

                // Получим массив горизонтальных слайсов с изображения
                var points = GetObjectDoublePoints(bmp, step);

                // Нормализуем массив слайсов
                NormalizeTestPoints(ref points);

                // Добавим его как секцию
                sections.Add(points);

                //Выведем в консоль информацию о конкретной секции
                // Имя файла
                // Количество горизонтальных слайсов
                // Порядковый номер изображения
                // Наибольшая разница между горизонтальными координатами (ширина модели)

                // что-то странное, надо понять, что это вообще
                // Наименьший слайс, в котором левая и правая координаты равны
                Console.WriteLine("{0}: {1}, alpha: {2}, max size: {3}, min size: {4}",
                    shortName,
                    points.Count,
                    alpha,
                    points.Max(point => point.X1 - point.X0),
                    points.Min(point => point.X1 = point.X0));
            }
        }

        // Лист с размерами
        var sizes = new List<int>();

        // Обход по всем секциям
        foreach (var section in sections)
        {
            // аналогично, что за максимальный слайс в секции, у которого равны координаты?
            // Запись в размер наибольшего элемента, у которого равны горизонтальные координаты
            sizes.Add(section.Max(line => line.X1 = line.X0));
        }

        // Наименьший размер модели по горизонтали и его индекс
        var minSize = sizes.Min();
        var minSecIndex = sizes.IndexOf(minSize);
        Console.WriteLine("Min size: {0}; min size index {1}, alpha: {2}",
            minSize,
            minSecIndex,
            minSecIndex * alphastep);

        // Наибольший размер по горизонтали и его индекс
        var maxSize = sizes.Max();
        var maxSecIndex = sizes.IndexOf(maxSize);
        Console.WriteLine("Max size: {0}; max size index {1}, alpha: {2}",
            maxSize,
            maxSecIndex,
            maxSecIndex * alphastep);


        // Размер по вертикали
        var ySize = sections.Max(section => section.Last().Y - section.First().Y);


        // Создание облака точек в форме куба в размер
        Console.WriteLine("Make Virtual Object");
        var virtualObject = MakeVirtualObject(step, ySize, minSize, maxSize);

        // Первый вырез проекции в облако точек
        Console.WriteLine("Make Cuts");
        MakeFirstCut(ref virtualObject, sections[minSecIndex]);


        // Проход по углам с шагом
        for (var i = 0; i < (100 / alphastep); i++)
        {
            var j = i + minSecIndex;
            if (j >= sections.Count)
                j -= sections.Count;

            // Последующие вырезания проекций из облака точек
            MakeSecondCut(ref virtualObject, sections[j], step, alphastep * i, 0);
            Console.WriteLine("Size: {0}", virtualObject.Count);
        }

        // Удаление внутренних точек из облака
        Console.WriteLine("Remove Inside");
        RemoveInside(ref virtualObject);

        // Сжатие облака точек
        ShrinkVirtualObject(ref virtualObject, step);

        // Сохранение в формате XYZ
        Console.WriteLine("Save XYZ");
        SaveXyz(virtualObject, path + "\\points");

        // Вокселизация и сохранение в формате STL
        Console.WriteLine("Save STL");
        SaveStl(virtualObject, Path + "\\points");
    }
}
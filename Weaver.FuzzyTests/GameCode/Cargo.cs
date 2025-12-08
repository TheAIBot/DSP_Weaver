using System.Numerics;

namespace Weaver.FuzzyTests.GameCode;

public struct Cargo
{
    public byte stack;

    public byte inc;

    public short item;

    public const int kIncLevel1 = 1;

    public const int kIncLevel2 = 2;

    public const int kIncLevel3 = 4;

    public static int[] accTable = new int[11]
    {
        0, 250, 500, 750, 1000, 1250, 1500, 1750, 2000, 2250,
        2500
    };

    public static int[] incTable = new int[11]
    {
        0, 125, 200, 225, 250, 275, 300, 325, 350, 375,
        400
    };

    public static double[] accTableMilli = new double[11]
    {
        0.0, 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0, 2.25,
        2.5
    };

    public static double[] incTableMilli = new double[11]
    {
        0.0, 0.125, 0.2, 0.225, 0.25, 0.275, 0.3, 0.325, 0.35, 0.375,
        0.4
    };

    public static int[] incFastDivisionNumerator = new int[11]
    {
        40, 45, 48, 49, 50, 51, 52, 53, 54, 55,
        56
    };

    public const int incFastDivisionDenominator = 40;

    public static int[] powerTable = new int[11]
    {
        0, 300, 700, 1100, 1500, 1900, 2300, 2700, 3100, 3500,
        3900
    };

    public static double[] powerTableRatio = new double[11]
    {
        1.0, 1.3, 1.7, 2.1, 2.5, 2.9, 3.3, 3.7, 4.1, 4.5,
        4.9
    };

    public const int kIncLevelMax = 10;

    public const int kSprayIncMax = 4;

    public static byte[] fastIncArrowTable = new byte[11]
    {
        0, 1, 2, 2, 3, 3, 3, 3, 3, 3,
        3
    };

    public const int dataLen = 32;

    public Cargo(short _item, Vector3 _pos, Quaternion _rot)
    {
        stack = 1;
        inc = 0;
        item = _item;
    }

    public void SetEmpty()
    {
        stack = 1;
        inc = 0;
        item = 0;
    }

    public ItemPackage GetItemPackage()
    {
        return new ItemPackage(stack, item, inc);
    }
}

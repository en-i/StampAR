using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using OpenCvSharp;
using OpenCvSharp.Demo;

public class StampScript : MonoBehaviour
{
    //UIが貼り付けられたキャンバス
    public GameObject canvas;
    //プレビュー領域
    public RawImage preview;
    //キャプチャ領域を保持
    UnityEngine.Rect capRect;
    //キャプチャ画像を保持
    Texture2D capTexture;
    //OpenCVで扱う画像を保持(Texture2Dのままでは使用できない)
    Mat bgraMat, binMat;
    //14色の色情報
    byte[,] colors = { { 255, 255, 255 }, { 18, 0, 230 }, { 0, 152, 243 }, { 0, 241, 255 }, { 31, 195, 143 }, { 68, 153, 0 }, { 150, 158, 0 }, { 233, 160, 0 }, { 183, 104, 0 }, { 136, 32, 29 }, { 131, 7, 146 }, { 127, 0, 228 }, { 79, 0, 229 }, { 0, 0, 0 } };
    //何番目の色かを表す変数(0~13)
    int colorNo = 0;
    public GameObject original;
    // Start is called before the first frame update
    void Start()
    {
        int w = Screen.width;
        int h = Screen.height;
        //原点(0,0)から画面の縦横の長さまでをキャプチャ領域とする
        capRect = new UnityEngine.Rect(0, 0, w, h);
        //画面サイズの空画像を作成
        capTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        //capTextureをプレビュー領域に貼り付け
        preview.material.mainTexture = capTexture;
    }
    public void PutObject()
    {
        //カメラ(自分)の位置を取得
        Camera cam = Camera.main;
        //3つの座標を取得することで縦横の長さを計測する
        //画面左下のxy座標を3次元に変換
        Vector3 v1 = cam.ViewportToWorldPoint(new Vector3(0, 0, 0.6f));
        //画面右上のxy座標を3次元に変換
        Vector3 v2 = cam.ViewportToWorldPoint(new Vector3(1, 1, 0.6f));
        //画面左上のxy座標を3次元に変換
        Vector3 v3 = cam.ViewportToWorldPoint(new Vector3(0, 1, 0.6f));
        //キャプチャ領域の実空間でのサイズを計算
        float w = Vector3.Distance(v2, v3);
        float h = Vector3.Distance(v1, v3);
        GameObject stamp = GameObject.Instantiate(original);
        //オブジェクトの生成とカメラに対する位置・向き・サイズを設定
        stamp.transform.parent = cam.transform;
        //60cm手前
        stamp.transform.localPosition = new Vector3(0, 0, 0.6f);
        stamp.transform.localRotation = Quaternion.identity;
        stamp.transform.localScale = new Vector3(w, h, 1);
        //作ったオブジェクトに貼るテクスチャを作成
        Texture2D stampTexture = new Texture2D(capTexture.width, capTexture.height);
        //色を塗り、テクスチャとして貼り付ける
        SetColor(stampTexture);
        stamp.GetComponent<Renderer>().material.mainTexture = stampTexture;
        //スタンプの原点をカメラではなくワールドに変更(カメラを動かしてもついてこなくなる)
        stamp.transform.parent = null;
        preview.enabled = false;
    }
    IEnumerator ImageProcessing()
    {
        //canvas上のUIを一時的に消す
        canvas.SetActive(false);
        //Mat用に確保したメモリを解放する(解放しないと一々作ってしまうのでメモリを食いつぶす)
        if (bgraMat != null) { bgraMat.Release(); }
        if (binMat != null) { binMat.Release(); }
        //フレーム終了を待つ
        yield return new WaitForEndOfFrame();
        //画像の生成
        CreateImages();
        //テクスチャに色をセット
        SetColor(capTexture);
        //canvas上のUIを再表示
        canvas.SetActive(true);
        //プレビューを表示する
        preview.enabled = true;
    }

    void CreateImages()
    {
        //キャプチャ開始
        capTexture.ReadPixels(capRect, 0, 0);
        //各画素の色をテクスチャに反映
        capTexture.Apply();
        //Texture2DをMatに変換
        bgraMat = OpenCvSharp.Unity.TextureToMat(capTexture);
        //カラー画像をグレースケール(濃淡)画像に変換
        binMat = bgraMat.CvtColor(ColorConversionCodes.BGRA2GRAY);
        //二値化、結果を白黒反転
        binMat = binMat.Threshold(100, 255, ThresholdTypes.Otsu);
        //あとで色を変えられるようにカラー(BGR)に変換
        bgraMat = binMat.CvtColor(ColorConversionCodes.GRAY2BGRA);
    }
    public void StartCV()
    {
        //コルーチンの実行
        StartCoroutine(ImageProcessing());
    }
    public void ChangeColor()
    {
        colorNo++;
        colorNo %= 14;
        SetColor(capTexture);
    }
    void SetColor(Texture2D texture)
    {
        //Matが初期化されていない場合は何もしない
        if (bgraMat == null || binMat == null) { return; }
        unsafe
        {
            //各Matのピクセル情報の配列(ポインタ)を取得
            //カラーの情報を格納する
            byte* bgraPtr = bgraMat.DataPointer;
            //白黒の情報を格納する
            byte* binPtr = binMat.DataPointer;
            //全ピクセル数を算出
            int pixelCount = binMat.Width * binMat.Height;
            //各ピクセルを参照して黒画素なら色を塗る
            for (int i = 0; i < pixelCount; i++)
            {
                //白黒画像のi番目に相当するBGRAのデータの位置
                int bgraPos = i * 4;
                //白かったら無視(透過させる)
                if (binPtr[i] == 255)
                {
                    bgraPtr[bgraPos + 3] = 0;
                }
                //黒かったら色を塗る
                else
                {
                    bgraPtr[bgraPos] = colors[colorNo, 0];//B
                    bgraPtr[bgraPos + 1] = colors[colorNo, 1];//G
                    bgraPtr[bgraPos + 2] = colors[colorNo, 2];//R
                    bgraPtr[bgraPos + 3] = 255;
                }
            }
        }
        OpenCvSharp.Unity.MatToTexture(bgraMat, texture);
    }

    // Update is called once per frame
    void Update()
    {

    }
}

/// <reference lib="webworker" />

addEventListener('message', ({ data }) => {

  // 文字列string を数値にする
  const toNumber = (num: string) => {
    let result: number = null;
    try {
      const tmp: string = num.toString().trim();
      if (tmp.length > 0) {
        result = ((n: number) => isNaN(n) ? null : n)(+tmp);
      }
    } catch {
      result = null;
    }
    return result;
  };


  const jsonData = data.jsonData;
  const reac = {};
  const max_value = {};
  const value_range = {};
  let error: any = null;

  try {
    for (const caseNo of Object.keys(jsonData)) {
      const target = new Array();
      const caseData: {} = jsonData[caseNo];

      // 存在チェック
      if (typeof (caseData) !== 'object') {
        continue;
      }
      if (!('reac' in caseData)) {
        continue;
      }
      const json: {} = caseData['reac'];
      if (json === null) {
        continue;
      }

      let values = {max_d: Number.MIN_VALUE, max_r: Number.MIN_VALUE,
                    min_d:  Number.MAX_VALUE, min_r:  Number.MAX_VALUE,
                    max_d_m: '0' , max_r_m: '0' ,
                    min_d_m: '0' , min_r_m: '0' , }

      for (const n of Object.keys(json)) {
        const item: {} = json[n];

        let tx: number = toNumber(item['tx']);
        let ty: number = toNumber(item['ty']);
        let tz: number = toNumber(item['tz']);
        let mx: number = toNumber(item['mx']);
        let my: number = toNumber(item['my']);
        let mz: number = toNumber(item['mz']);

        // バージョン2.4系からは計算結果の符号そのままで表示する
        // 支点反力の正は全体座標系の正の向きおよび右ねじまわりを正とする
        const result = {
          id: n.replace('node', ''),
          tx: (tx == null) ? 0 : tx,
          ty: (ty == null) ? 0 : ty,
          tz: (tz == null) ? 0 : tz,
          mx: (mx == null) ? 0 : mx,
          my: (my == null) ? 0 : my,
          mz: (mz == null) ? 0 : mz
        };
        target.push(result);
            
        // 最大値を記録する three.js で使う
        for (const v of [tx, ty, tz]) {
          if (values.max_d < v) {
            values.max_d = v;
            values.max_d_m = n;
          }
          if (values.min_d > v) {
            values.min_d = v;
            values.min_d_m = n;
          }
        }
        for (const v of [mx, my, mz]) {
          if (values.max_r < v) {
            values.max_r = v;
            values.max_r_m = n;
          }
          if (values.min_r > v) {
            values.min_r = v;
            values.min_r_m = n;
          }
        }

      }
      const No: string = caseNo.replace("Case", "");
      reac[No] = target;
      max_value[No] = Math.max(Math.abs(values.max_d), Math.abs(values.min_d));
      value_range[No] = values;
    }
  } catch (e) {
    error = e;
  }

  postMessage({ reac, error, max_value, value_range });
});

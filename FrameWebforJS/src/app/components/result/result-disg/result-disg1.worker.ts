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
  const disg = {};
  const max_value = {};
  const value_range = {};
  let error: any = null;

  try {


    for (const caseNo of Object.keys(jsonData)) {
      const target = new Array();
      const caseData: {} = jsonData[caseNo];

      // 存在チェック
      if (typeof caseData !== "object") {
        continue;
      }
      if (!("disg" in caseData)) {
        continue;
      }
      const json: {} = caseData["disg"];

      let values = {max_d: Number.MIN_VALUE, max_r: Math.PI * -1000,
                    min_d: Number.MAX_VALUE, min_r: Math.PI *  1000,
                    max_d_m: '0' , max_r_m: '0' ,
                    min_d_m: '0' , min_r_m: '0' ,}

      for (const n of Object.keys(json)) {

        const id = n.replace("node", "");
        if (id.includes('n')) {
          continue; // 着目節点は除外する
        }
        if (id.includes('l')) {
          continue; // 荷重による分割点は除外する
        }

        const item: {} = json[n];

        let dx: number = toNumber(item["dx"]);
        let dy: number = toNumber(item["dy"]);
        let dz: number = toNumber(item["dz"]);
        let rx: number = toNumber(item["rx"]);
        let ry: number = toNumber(item["ry"]);
        let rz: number = toNumber(item["rz"]);
        dx = dx == null ? 0 : dx * 1000;
        dy = dy == null ? 0 : dy * 1000;
        dz = dz == null ? 0 : dz * 1000;
        rx = rx == null ? 0 : rx * 1000;
        ry = ry == null ? 0 : ry * 1000;
        rz = rz == null ? 0 : rz * 1000;
        const result = {
          id: id,
          dx: dx,
          dy: dy,
          dz: dz,
          rx: rx,
          ry: ry,
          rz: rz,
        };
        target.push(result);
    
        // 最大値を記録する three.js で使う
        for (const v of [dx, dy, dz]) {
          if (values.max_d < v) {
            values.max_d = v;
            values.max_d_m = n;
          }
          if (values.min_d > v) {
            values.min_d = v;
            values.min_d_m = n;
          }
        }
        for (const v of [rx, ry, rz]) {
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
      disg[No] = target;
      max_value[No] = Math.max(Math.abs(values.max_d), Math.abs(values.min_d));
      value_range[No] = values;
    }

  } catch (e) {
    error = e;
  }

  postMessage({ disg, max_value, value_range, error });


});

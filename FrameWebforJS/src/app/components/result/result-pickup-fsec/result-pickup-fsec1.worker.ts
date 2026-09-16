/// <reference lib="webworker" />

addEventListener('message', ({ data }) => {

  const pickList = data.pickList;
  const fsecCombine = data.fsecCombine;
  const fsecPickup = {};
  const max_values = {};

  // pickupのループ
  for (const pickNo of Object.keys(pickList)) {
    const max_value = {
      fx: 0, fy: 0, fz: 0,
      mx: 0, my: 0, mz: 0
    }

    const combines: any[] = pickList[pickNo];
    let tmp: {} = null;
    for (const combNo of combines) {
      const com = JSON.parse(
        JSON.stringify({
          temp: fsecCombine[combNo]
        })
      ).temp;

      if (com == null || com == undefined) {
        console.log('Combine' + combNo + 'がない');
        continue;
      }

      if (tmp == null) {
        tmp = com;
        for (const k of Object.keys(com)) { // 最大値を 集計する
          for (const value of tmp[k]) {
            max_value.fx = Math.max(Math.abs(value.fx), max_value.fx);
            max_value.fy = Math.max(Math.abs(value.fy), max_value.fy);
            max_value.fz = Math.max(Math.abs(value.fz), max_value.fz);
            max_value.mx = Math.max(Math.abs(value.mx), max_value.mx);
            max_value.my = Math.max(Math.abs(value.my), max_value.my);
            max_value.mz = Math.max(Math.abs(value.mz), max_value.mz);
          }
        }
        continue;
      }
      for (const k of Object.keys(com)) {
        const key = k.split('_');
        const target = com[k];
        const comparison = tmp[k];
        for (const id of Object.keys(comparison)) {
          const a = comparison[id];
          if (!(id in target)) {
            continue;
          }
          const b = target[id];
          if (key[1] === 'max') {
            if (b[key[0]] > a[key[0]]) {
              tmp[k][id] = com[k][id];
            }
          } else {
            if (b[key[0]] < a[key[0]]) {
              tmp[k][id] = com[k][id];
            }
          }
        }

        // 最大値を 集計する
        for (const value of tmp[k]) {
          max_value.fx = Math.max(Math.abs(value.fx), max_value.fx);
          max_value.fy = Math.max(Math.abs(value.fy), max_value.fy);
          max_value.fz = Math.max(Math.abs(value.fz), max_value.fz);
          max_value.mx = Math.max(Math.abs(value.mx), max_value.mx);
          max_value.my = Math.max(Math.abs(value.my), max_value.my);
          max_value.mz = Math.max(Math.abs(value.mz), max_value.mz);
        }
      }
    }
    fsecPickup[pickNo] = tmp;
    max_values[pickNo] = max_value;
    tmp = null;
  }

  const value_range = {};
  // CombineNoごとの最大最小を探す
  for (const combNo of Object.keys(fsecPickup)) {
    const caseData = fsecPickup[combNo];

    if (caseData == null || caseData == undefined) {
      console.log('CaseData' + combNo + 'がない');
      continue;
    }
    const key_list = Object.keys(caseData);
    const values = {};
    // dx～rzの最大最小をそれぞれ探す
    for (const key of key_list) {
      const datas = caseData[key];
     /* */  let key2: string;
      if (key.includes('fx')) {
        key2 = 'fx';
      } else if (key.includes('fy')) {
        key2 = 'fy';
      } else if (key.includes('fz')) {
        key2 = 'fz';
      } else if (key.includes('mx')) {
        key2 = 'mx';
      } else if (key.includes('my')) {
        key2 = 'my';
      } else if (key.includes('mz')) {
        key2 = 'mz';
      }
      let targetValue = (key.includes('max')) ? Number.MIN_VALUE: Number.MAX_VALUE;
      let targetValue_m = '0';
      if (key.includes('max')) {  // 最大値を探す
        //for (const row of Object.keys(datas)) {
        for (let num = 0; num < datas.length; num++) {
          const row = num.toString();
          const data = datas[row][key2];
          if (data >= targetValue) {
            targetValue = data;
            // memberNoがないとき(着目点が最大)の分岐
            if (datas[row].m === '') {
              let m_no: string
              for (let num2 = num - 1; num2 > 0; num2--) {
                const row2 = num2.toString();
                if (datas[row2].m !== '') {
                  m_no = datas[row2].m;
                  break;
                }
              }
              targetValue_m = m_no;
            } else {
              targetValue_m = datas[row].m;
            }
          }
        }
      } else {  // 最小値を探す
        // for (const row of Object.keys(datas)) {
        for (let num = 0; num < datas.length; num++) {
          const row = num.toString();
          const data = datas[row][key2];
          if (data <= targetValue) {
            targetValue = data;
            // memberNoがないとき(着目点が最小)の分岐
            if (datas[row].m === '') {
              let m_no: string
              for (let num2 = num - 1; num2 > 0; num2--) {
                const row2 = num2.toString();
                if (datas[row2].m !== '') {
                  m_no = datas[row2].m;
                  break;
                }
              }
              targetValue_m = m_no;
            } else {
              targetValue_m = datas[row].m;
            }
          }
        }
      }
      if (Math.abs(targetValue) === Number.MAX_VALUE) {
        continue;
      }
      values[key] = {max: targetValue, max_m: targetValue_m};
    }
    if (Object.keys(values).length === 0) {
      continue;
    }

    const values2 = {
      x: {
        max_d: 0, max_d_m: 0,
        min_d: 0, min_d_m: 0,
        max_r: 0, max_r_m: 0,
        min_r: 0, min_r_m: 0,
      },
      y: {
        max_d: 0, max_d_m: 0,
        min_d: 0, min_d_m: 0,
        max_r: 0, max_r_m: 0,
        min_r: 0, min_r_m: 0,
      },
      z: {
        max_d: 0, max_d_m: 0,
        min_d: 0, min_d_m: 0,
        max_r: 0, max_r_m: 0,
        min_r: 0, min_r_m: 0,
      }
    }
    for(const key of Object.keys(values2)){
      let kf = 'f' + key + '_max';
      if(kf in values){
        values2[key].max_d = values[kf].max;
        values2[key].max_d_m = values[kf].max_m
      }
      kf = 'f' + key + '_min';
      if(kf in values){
        values2[key].min_d = values[kf].max;
        values2[key].min_d_m = values[kf].max_m
      }
      let km = 'm' + key + '_max';
      if(km in values){
        values2[key].max_r = values[km].max;
        values2[key].max_r_m = values[km].max_m
      }
      km = 'm' + key + '_min';
      if(km in values){
        values2[key].min_r = values[km].max;
        values2[key].min_r_m = values[km].max_m
      }
    }

    value_range[combNo] = values2;
  }

  postMessage({ fsecPickup, max_values, value_range });
});

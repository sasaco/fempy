/// <reference lib="webworker" />

addEventListener('message', ({ data }) => {

  const pickList = data.pickList;
  const disgCombine = data.disgCombine;
  const disgPickup = {};

  // pickupのループ
  for (const pickNo of Object.keys(pickList)) {
    const combines: any[] = pickList[pickNo];
    let tmp: {} = null;
    for (const combNo of combines) {
      const com = JSON.parse(
        JSON.stringify({
          temp: disgCombine[combNo]
        })
      ).temp;
      if (tmp == null) {
        tmp = com;
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
      }
    }
    if (tmp !== null) {
      disgPickup[pickNo] = tmp;
    }
  }

  const value_range = {};
  // CombineNoごとの最大最小を探す
  for (const combNo of Object.keys(disgPickup)) {
    const caseData = disgPickup[combNo];

    if (caseData == null || caseData == undefined) {
      console.log('CaseData' + combNo + 'がない');
      continue;
    }
    
    const key_list = Object.keys(caseData);
    const values_d = {};
    const values_r = {};
    // dx～rzの最大最小をそれぞれ探す
    for (const key of key_list) {
      const datas = caseData[key];
      let key2: string;
      let is_d = false;
      if (key.includes('dx')) {
        key2 = 'dx';
        is_d = true;
      } else if (key.includes('dy')) {
        key2 = 'dy';
        is_d = true;
      } else if (key.includes('dz')) {
        key2 = 'dz';
        is_d = true;
      } else if (key.includes('rx')) {
        key2 = 'rx';
      } else if (key.includes('ry')) {
        key2 = 'ry';
      } else if (key.includes('rz')) {
        key2 = 'rz';
      }
      let targetValue = (key.includes('max')) ? Number.MIN_VALUE : Number.MAX_VALUE;
      let targetValue_m = '0';
      if (key.includes('max')) {
        for (const row of Object.keys(datas)) {
          const data = datas[row][key2];
          if (data >= targetValue) {
            targetValue = data;
            targetValue_m = row;
          }
        }
      } else {
        for (const row of Object.keys(datas)) {
          const data = datas[row][key2];
          if (data <= targetValue) {
            targetValue = data;
            targetValue_m = row;
          }
        }
      }
      if (Math.abs(targetValue) === Number.MAX_VALUE) {
        continue;
      }
      if(is_d){
        values_d[key] = {max: targetValue, max_m: targetValue_m};
      } else{
        values_r[key] = {max: targetValue, max_m: targetValue_m};
      }
    }
    if (Object.keys(values_d).length === 0) {
      continue;
    }
    const values2 = {
      max_d : Number.MIN_VALUE, max_d_m: 0,
      min_d : Number.MAX_VALUE, min_d_m: 0,
      max_r : Number.MIN_VALUE, max_r_m: 0,
      min_r : Number.MAX_VALUE, min_r_m: 0
    };
    for(const key of Object.keys(values_d)){
      const value = values_d[key];
      if(value.max > values2.max_d){
        values2.max_d = value.max;
        values2.max_d_m = value.max_m;
      } else if(value.max < values2.min_d){
        values2.min_d = value.max;
        values2.min_d_m = value.max_m;
      }
    }
    for(const key of Object.keys(values_r)){
      const value = values_r[key];
      if(value.max > values2.max_r){
        values2.max_r = value.max;
        values2.max_r_m = value.max_m;
      } else if(value.max < values2.min_r){
        values2.min_r = value.max;
        values2.min_r_m = value.max_m;
      }
    }
    value_range[combNo] = values2;
  }
  postMessage({ disgPickup, value_range });
});

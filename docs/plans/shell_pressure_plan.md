# シェル要素面圧機能実装計画書

## 1. 背景と目的

### 1.1 現状分析
- **V0（JavaScript版）**: 面圧機能が完全に実装済み
- **現在のPython版**: 面圧機能が未実装
- **影響**: シェル要素を使用した構造解析で面圧荷重を考慮できない

### 1.2 実装目的
- シェル要素に面圧荷重を適用可能にする
- V0の技術をPython版に移植し、同等の機能を提供
- 構造解析の精度向上と適用範囲の拡大

## 2. 技術調査結果

### 2.1 V0での面圧実装状況 ✅
- **面圧条件クラス**: `Pressure`クラス（`v0/src/BoundaryCondition.js`）
- **面圧処理**: `loadVector`関数で等価節点荷重に変換
- **データ構造**: `要素番号, 面番号, 圧力値`の形式
- **等価節点荷重計算**: 形状関数と法線ベクトルを使用

### 2.2 現在のPython版の状況 ❌
- **基底クラス**: `BaseElement.get_equivalent_nodal_loads`は未実装
- **シェル要素**: `ShellElement`で面圧処理が未実装
- **境界条件**: 面圧データの管理機能なし
- **ソルバー**: 面圧処理の呼び出しはあるが実装なし

## 3. 実装計画

### 3.1 Phase 1: 基盤実装（1-2週目）

#### 3.1.1 面圧条件クラスの実装
**ファイル**: `src/fem/boundary_condition.py`
```python
class Pressure:
    """面圧条件クラス"""
    def __init__(self, element_id: int, face: str, pressure: float):
        self.element_id = element_id
        self.face = face  # "F1", "F2" など
        self.pressure = pressure
```

#### 3.1.2 境界条件クラスの拡張
**ファイル**: `src/fem/boundary_condition.py`
```python
class BoundaryCondition:
    def __init__(self):
        # 既存の属性
        self.restraints = []
        self.loads = []
        self.distributed_loads = []
        
        # 新規追加
        self.pressures = []  # 面圧条件リスト
```

#### 3.1.3 シェル要素の等価節点荷重計算実装
**ファイル**: `src/fem/elements/shell_element.py`
```python
def get_equivalent_nodal_loads(self, load_type: str, values: List[float], 
                             face: Optional[int] = None) -> np.ndarray:
    """面圧の等価節点荷重を計算"""
    if load_type != 'pressure':
        raise NotImplementedError("Only pressure loads are supported")
    
    # V0のアルゴリズムを移植
    # 1. 面の境界を取得
    # 2. 形状関数ベクトルを計算
    # 3. 法線ベクトルを計算
    # 4. 等価節点荷重を計算
```

### 3.2 Phase 2: ソルバー統合（3-4週目）

#### 3.2.1 荷重ベクトル組み立ての拡張
**ファイル**: `src/fem/solver.py`
```python
def assemble_load_vector(self, mesh: MeshModel, boundary: BoundaryCondition,
                       elements: Dict[int, Any]) -> np.ndarray:
    # 既存の節点荷重処理
    
    # 新規追加: 面圧処理
    for pressure in boundary.pressures:
        elem_id = pressure.element_id
        if elem_id in elements:
            element = elements[elem_id]
            equiv_loads = element.get_equivalent_nodal_loads(
                'pressure', [pressure.pressure], pressure.face
            )
            # 全体荷重ベクトルに加算
```

#### 3.2.2 ファイル入出力の拡張
**ファイル**: `src/fem/file_io.py`
```python
def _read_pressure_conditions(self, pressure_data: List[Dict]) -> List[Pressure]:
    """面圧条件の読み込み"""
    pressures = []
    for p_data in pressure_data:
        pressure = Pressure(
            element_id=p_data['element_id'],
            face=p_data['face'],
            pressure=p_data['pressure']
        )
        pressures.append(pressure)
    return pressures
```

### 3.3 Phase 3: テストと検証（5-6週目）

#### 3.3.1 単体テスト
**ファイル**: `tests/elements/test_shell_pressure.py`
```python
class TestShellPressure(unittest.TestCase):
    def test_pressure_equivalent_nodal_loads(self):
        """面圧の等価節点荷重計算テスト"""
        
    def test_pressure_direction(self):
        """面圧の方向性テスト"""
        
    def test_pressure_magnitude(self):
        """面圧の大きさテスト"""
```

#### 3.3.2 統合テスト
**ファイル**: `tests/integration/test_pressure_integration.py`
```python
class TestPressureIntegration(unittest.TestCase):
    def test_shell_with_pressure_load(self):
        """シェル要素に面圧を適用した解析テスト"""
        
    def test_pressure_vs_concentrated_load(self):
        """面圧と集中荷重の等価性テスト"""
```

## 4. 技術的詳細

### 4.1 V0の面圧処理アルゴリズム
```javascript
// V0の面圧処理（v0/src/Solver.js）
for(i=0;i<press.length;i++){
  var border=press[i].getBorder(model.mesh.elements[press[i].element]);
  var p=model.mesh.getNodes(border);
  var ps=border.shapeFunctionVector(p,press[i].press);
  var norm=normalVector(p);
  var count=border.nodeCount();
  for(j=0;j<count;j++){
    index0=index[border.nodes[j]];
    vector[index0]-=ps[j]*norm.x;    // X方向荷重
    vector[index0+1]-=ps[j]*norm.y;  // Y方向荷重
    vector[index0+2]-=ps[j]*norm.z;  // Z方向荷重
  }
}
```

### 4.2 Python版への移植方針
1. **形状関数ベクトル計算**: V0の`shapeFunctionVector`を移植
2. **法線ベクトル計算**: V0の`normalVector`を移植
3. **境界取得**: V0の`getBorder`を移植
4. **等価節点荷重計算**: V0のアルゴリズムをそのまま移植

### 4.3 データ構造
```json
{
  "pressures": [
    {
      "element_id": 1,
      "face": "F1",
      "pressure": 1000.0
    }
  ]
}
```

## 5. 実装スケジュール

| 週 | フェーズ | 主要タスク | 成果物 |
|:--|:--|:--|:--|
| 1-2 | Phase 1 | 基盤実装 | 面圧クラス、境界条件拡張 |
| 3-4 | Phase 2 | ソルバー統合 | 荷重ベクトル組み立て、ファイル入出力 |
| 5-6 | Phase 3 | テストと検証 | 単体テスト、統合テスト、ドキュメント |

## 6. リスクと対策

### 6.1 技術的リスク
- **V0のアルゴリズムの複雑性**: 段階的な移植とテストで対応
- **数値精度の問題**: V0と同じ計算結果になるよう注意深く実装
- **パフォーマンス**: プロファイリングと最適化で対応

### 6.2 対策
- V0のテストケースをPython版でも実行
- 段階的な実装とテスト
- コードレビューによる品質確保

## 7. 期待される効果

### 7.1 機能面
- シェル要素での面圧荷重解析が可能
- V0と同等の解析精度
- より幅広い構造解析への対応

### 7.2 技術面
- V0の技術のPython版への移植完了
- モジュール化された設計の維持
- 将来の機能拡張への基盤整備

## 8. 今後の拡張計画

### 8.1 短期（6ヶ月以内）
- 面圧の可視化機能
- 面圧の後処理機能
- 面圧の最適化機能

### 8.2 長期（1年以内）
- 動的面圧の対応
- 非線形面圧の対応
- 面圧の感度解析

## 9. まとめ

本計画書では、V0で実装済みの面圧機能をPython版に移植する詳細な計画を提示しました。段階的な実装により、リスクを最小化しながら、シェル要素での面圧荷重解析機能を実現します。

実装完了により、FrameWeb3はより包括的な構造解析システムとしての価値を提供できるようになります。
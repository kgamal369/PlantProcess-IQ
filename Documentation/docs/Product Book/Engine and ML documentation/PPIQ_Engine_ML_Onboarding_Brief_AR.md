# PPIQ - ملخص تعريفي للمهندس الجديد
## مهمة: الـ Engine و الـ AI/ML Models

**اقرأ ده الأول قبل أي كود. الوقت المتوقع: ساعة.**
**المرجع الكامل: `PPIQ_Layer_B_Architecture_Design_Pack.md` (Revision 7). الملف ده ملخص، مش بديل.**
**الحالة: IMPLEMENTATION DESIGN FROZEN. مربوط على Chapters 2/3/4 مباشرة.**

---

## 1. المنتج بيعمل ايه

PPIQ منتج بيقرأ بيانات مصنع (أي مصنع) ويجاوب على نوعين أسئلة:

- **سؤال حقيقة**: كام قطعة اتعملت الشهر ده؟ كام واحدة فيهم فيها عيب؟
- **سؤال ذكاء**: ليه العيوب زادت؟ إيه اللي بيسببها؟ القطعة دي هتطلع كويسة ولا لأ؟ إيه أفضل طريقة تشغيل؟

النوع الأول اسمه **Layer A**. النوع التاني اسمه **Layer B**، وده شغلك.

---

## 2. القاعدة الأولى اللي كل حاجة مبنية عليها

**المنتج generic. يعني كود المحرك ما يعرفش صناعة العميل.**

عملاؤنا القادمين: بترول، ومياه معدنية. وبعدين ممكن ورق، دواء، إطارات، أسمنت.

يعني ممنوع منعاً باتاً في كود Layer B:

```
ممنوع:  if (customer == "steel")
ممنوع:  read table "Coil"
ممنوع:  column named "heat_id"
ممنوع:  class OilModel
```

المحرك بيشتغل بـ **codes** مش بأسماء جداول. العميل هو اللي بيقول "الجدول ده معناه كذا" مرة واحدة، والمحرك بيقرأ المعنى بس.

القاعدة دي اسمها **الـ Semantic Wall**، وهي أهم حاجة في التصميم.

---

## 3. الصورة الكبيرة

```
  بيانات العميل (Oracle, SQL Server, historian, Excel, API)
        |
        v
  [1] DUMP STORE            البيانات زي ما وصلت بالظبط
        |
        |   مهندس العميل بيرسم على canvas ويقول:
        |   "العمود ده = سرعة"، "الجدولين دول نفس القطعة"
        v
  [2] SEMANTIC MODEL        العقد. مُصدَّر ومجمّد ومش بيتغير
      plant_relationships   العلاقات والـ joins. بتتنشر مع الـ transformation
        |
        v
  [3] CANONICAL PLANT DATA  البيانات بعد ما اتفهمت
        |
  ====== SEMANTIC WALL ====== من هنا وطالع مفيش أسماء جداول
        |
        v
  [4] SNAPSHOTS             نسخة مجمّدة (immutable) للتدريب
        |
        v
  [5] الـ ENGINES           إحصاء + ML
        |
        v
  [6] النتائج               predictions, findings, evidence
        |
  ====== SERVING WALL ====== اتجاه واحد بس
        |
        v
  [7] Dashboards + Assistant
```

**النقطة الأهم في الرسمة دي**: خطوة [2]. هي اللي بتخلي المنتج generic. من غيرها المنتج بيبقى مشروع لعميل واحد.

---

## 4. الحيطتين (لازم تفهمهم كويس)

### Semantic Wall
مفيش أي component بعد الحيطة دي يعرف اسم جدول عند العميل. لو لقيت نفسك بتكتب اسم جدول في كود Layer B، انت كسرت التصميم.

### Serving Wall
فيه حالتين للنظام:

```
  TRAINING STATE                    SERVING STATE
  --------------                    -------------
  GPU, ساعات، بيانات كاملة          CPU، ثواني، بيانات جاهزة
  بيدرّب models                      بيقرأ models جاهزة
  بيشتغل بالليل والويك اند           بيشتغل طول اليوم
        |
        +--- promotion (اتجاه واحد) --->

  ممنوع تماماً: request من المستخدم يدخل جوه training
```

السبب: لو request مستخدم قدر يدخل الـ training pipeline، الـ 2 minutes ceiling بيبقى كلام فاضي.

---

## 5. اللغات - ليه كل واحدة

| اللغة | فين | ليه |
|---|---|---|
| **C# / .NET 9** | الـ platform كله: API, orchestration, job runtime, Layer A, الـ registry | المنتج أصلاً .NET. والـ business logic والـ governance والـ gates كلها هنا |
| **Python** | الـ runtime الأساسي للـ data science والـ ML: تدريب PyTorch، الـ encoders، الـ boosting، الـ SHAP، وبعض التطبيقات الإحصائية المتقدمة | أقوى منظومة مكتبات للـ ML |
| **SQL / PostgreSQL** | التنفيذ العلائقي المحكوم والـ data products | جزء من الإحصاء ممكن ينفذ managed وجزء in-database تحت **نفس المواصفة الرياضية ونفس عقد النتيجة** (`compute_runs.engine_placement`) |
| **TypeScript / React** | الـ canvas والـ Page Builder | الواجهة |

### إزاي C# و Python بيتكلموا

**مش بـ API كتير الطلبات.** الطريقة:

```
C# job runner
   -> بيكتب job spec (JSON) + بيحدد الـ snapshot
   -> بيشغّل Python process
                      |
                      v
              Python بيقرأ الـ snapshot (Parquet)
              بيدرّب / بيحسب
              بيكتب artifacts + results
                      |
   <- بيرجع exit code + manifest
C# بيقرأ النتيجة، بيشغّل الـ gates، وبيقرر publish ولا لأ
```

**القرار والحوكمة في C#. الحساب في Python.** ده مش تفضيل، ده عشان الـ gates والـ refusal rules لازم تبقى في مكان واحد وتحت اختبار.

---

## 6. الـ Models - إيه بيجاوب على إيه

سبع عائلات. كل واحدة ليها سؤال مختلف:

| # | الاسم | السؤال | التقنية | محتاج labels؟ |
|---|---|---|---|---|
| **MF-01** | Process Encoder | إيه شكل رحلة الإنتاج دي؟ | PyTorch (1D CNN أو Transformer) | **لأ** |
| **MF-02** | Similarity Index | إيه القطع القديمة اللي تشبه دي؟ | FAISS | **لأ** |
| **MF-03** | Novelty Model | هل الوضع ده غريب؟ | density / isolation | **لأ** |
| **MF-04** | Supervised Outcome | القطعة دي هتطلع فيها عيب؟ | **LightGBM** | أيوه |
| **MF-05** | Effect / Envelope | لو غيّرت البارامتر ده، النتيجة تتغير؟ | matched comparison | أيوه |
| **MF-06 (DF9)** | Statistical Engine | البارامتر ده مرتبط بالعيب ده؟ | scipy / statsmodels | أيوه |
| **MF-07** | Practice Engine | إيه طريقة التشغيل اللي بتدي أحسن نتيجة؟ | canonical signatures | أيوه |

### النقطة اللي أغلب الناس بتغلط فيها

**الـ deep learning مش هو اللي بياخد القرار.**

```
   الـ Encoder (deep, PyTorch)
        |
        | بيطلع vector (128-256 رقم) لكل قطعة
        v
   بنحط الـ vector ده كأعمدة عادية جنب الـ features المحسوبة
        |
        v
   LightGBM هو اللي بيتنبأ  <--- القرار هنا
        |
        v
   SHAP بيشرح ليه          <--- الشرح هنا
```

**ليه كده؟** لأن المنتج evidence-grade. لازم كل رقم يبقى مردود لصفوف حقيقية وقابل للشرح. الـ neural network الصافي مش هيديك ده.

**وكمان**: الـ MF-01 اختياري. عميل عنده بيانات مجمّعة بس من غير time series، المحرك بيشتغل من غير encoder خالص.

### وابدأ منين

**ابدأ بـ MF-06 (الإحصاء)**. ليه:
- مش محتاج encoder
- مش محتاج genealogy
- مش محتاج GPU
- بيشتغل مع أفقر عميل عندنا
- وهو أسرع حاجة تقدر تعرضها قدام عميل

---

## 7. التوقيتات الثلاثة

```
  [1] COMMISSIONING - مرة واحدة عند التركيب
      المدة: ساعات لأيام
      بيبني: كل حاجة من الصفر
      شرط: كل مرحلة checkpointed. لو وقع في الساعة 19
            يكمل من آخر مرحلة، ما يبدأش من الأول

  [2] WEEKLY - كل ويك اند
      الحد الأقصى: 24 ساعة (المخطط 10 ساعات، الباقي هامش)
      بيعمل: بيانات جديدة + إعادة تدريب + معايرة + نشر
      مهم جداً: الـ ENCODER مش بيتدرب أسبوعياً
      لو الوقت خلص: فيه abort ladder بيرمي الأقل أهمية

  [3] DAYTIME - طول ساعات الشغل
      ممنوع أي training نهائياً
      Tier 1: قراءة جاهزة        < 1 ثانية
      Tier 2: حساب محدود          < 30 ثانية
      Tier 3: أغلى من كده         بيرجع "هعملها job" فوراً
      السقف المطلق: أقل من دقيقتين
```

### ليه الـ encoder مجمّد

لو درّبته كل أسبوع، الـ vector space بيتغير. يعني كل علاقات التشابه القديمة بتبطل، والـ index لازم يتبني من أول وجديد، والمقارنة اللي عملها المستخدم الاثنين اللي فات مش هتتكرر.

بيتدرب كل 3 شهور، أو لما تتغير الأجهزة، وساعتها بنعيد كل حاجة مرة واحدة atomically.

---

## 8. حاجات ممنوعة - اكسر أي واحدة فيهم والتصميم بيقع

| # | القاعدة | ليه |
|---|---|---|
| **1** | ما تكتبش اسم جدول عميل في كود Layer B | Semantic Wall |
| **2** | ما تعملش join بنفسك. استخدم `RelationshipResolver` اللي بيقرأ من `plant_relationship_paths` بس | join غلط بيدي أرقام معقولة وغلط للأبد ومحدش يقدر يكتشفها |
| **3** | ما تستخدمش feature ظهرت **بعد** لحظة التنبؤ | ده اسمه leakage. بيديك دقة 99% وموديل مالوش أي قيمة |
| **4** | ما تدربش على جدول متغير. درّب على snapshot مختوم بس | من غير كده مش هتعرف تعيد إنتاج الموديل |
| **5** | لو مفيش method مناسبة، قول "مفيش method". ما تقولش "البيانات مالهاش تباين" | ده غلط حصل فعلاً قبل كده. المحرك اتهم بيانات العميل بدل ما يقول إنه ناقصه method |
| **6** | ما تعملش recommendation إلا بعد 9 checks كاملة | التوصية بتوصل لواحد في مصنع شغال. أربع checks بدل تسعة = تقليل هامش أمان |
| **7** | ما تجمعش (sum) احتمالات التنبؤ | الناتج بيبقى شكله رقم حقيقي وهو مش كده |
| **9** | ما تدربش من الـ `feature_store` مباشرة. اقرا من الـ snapshot artifact | الـ JSONB مش مسار التدريب. الاستثناء الوحيد هو الـ materialiser اللي بيعمل الـ snapshot |
| **10** | ما تخلّيش أي مخرج من LLM يبقى feature أو score | الحد مش المودالية، الحد هو الحوكمة. النص والصورة يدخلوا نتيجة متعلمة بس من خلال model definition كامل |
| **8** | الـ refusal بيتكتب كـ row في الداتابيز، مش exception | عشان الـ dashboard يعرض سبب مكتوب، مش شاشة فاضية |

---

## 9. حاجة مهمة في الفلسفة

المنتج ده **مش بيتحكم في المصنع**. أبداً.

```
  PPIQ بيكتب في:          PPIQ عمره ما بيكتب في:
  ------------           ---------------------
  الداتابيز بتاعته        نظام العميل
  سجل القرارات            MES / LIMS / historian
  التوقعات والأدلة        أي PLC أو DCS أو setpoint
```

لما المستخدم يضغط "Accept" على توصية، إحنا بنسجل **إن حد قرر**، مش بننفذ.

---

## 9.5 حاجتين لازم تفرق بينهم في الـ authoring

فيه **shell واحد** للتأليف، بس ليه أغراض مختلفة. متخلطش بينهم:

الـ shell واحد وله **خمس أغراض**:

```
  S1  Data Preparation   -> Transformation Definition
  S2  Widget/Page Binding-> Widget Definition
  S3  Analysis Authoring -> Analysis Definition   (احصاء، correlation)
  S4  Model Authoring    -> Model Definition      (feature + model blocks)
  S5  Plant Data Log     -> Rule Definition       (condition + action)
```

كل الـ definitions بتتخزن في مكان واحد: `definition_store` + `definition_versions` + جدول تفاصيل حسب النوع. **مفيش file authoritative ومفيش second source of truth.**

**الـ correlation مش زي الـ supervised model.** الأولى Method جوه تحليل. التانية artifact متدرب وله نسخة و feature engineering قبله و governance حواليه.

الاتنين بيمشوا بنفس السلوك:
`drag -> wire -> validate -> compile -> save versioned definition -> attach to job -> run`

---

## 9.55 الموديلات بتتفعّل إزاي

مفيش حاجة اسمها bundle. الجدول الحاكم هو `ppiq_plant.model_registry`، والتفعيل بيتم لكل **serving identity**:

```
serving identity = ( tenant_id , model_code , outcome_code , grain_code )
serving version  = serving identity + model_version
```

الـ `outcome_code` و `grain_code` **جزء من هوية الموديل مش metadata**. موديل بيتنبأ بنتيجة واحدة على grain واحد مش بديل لموديل تاني.

محورين منفصلين:
- `status` = دورة الحياة: trained / rejected / active / review / retired
- `serving_role` = اعتماد التقديم: none / serving_fallback

وفيه constraint بيمنع إن نسخة واحدة تكون primary و fallback في نفس الوقت، لأن fallback هو أصلاً الـ primary معناه إنك مش عندك شبكة أمان.

---

## 9.6 الـ Job Pools

مفيش pool اسمه serving. الـ pools الرسمية:

```
  import       سحب البيانات الجديدة
  projection   بناء الـ spine والـ features والـ snapshots
  analysis     الاحصاء، الـ practices، الـ evidence
  ml           بيتقسم ل 3 lanes:
                 ml.training        التدريب. pre-emptible
                 ml.batch_scoring   scoring مجدول و backfill
                 ml.online_scoring  scoring لحظي. **سعة محجوزة**
                                    التدريب عمره ما ياخد منها
  report       تجهيز النتائج
  interactive  محجوز فيزيائياً لطلبات المستخدم. ممنوع حد ياخد منه
```

الـ scoring بيشتغل بسياسة `latest-only`: لو فيه طلب قديم في الطابور ووصل واحد أحدث لنفس القطعة، القديم بيتلغي. توقع قديم مالوش قيمة.

**ليه الـ online scoring في lane لوحده؟** لأن التوقع لازم يوصل قبل ما المرحلة اللي تقدر تصلح فيها المشكلة تعدّي. لو التدريب يقدر ياخد سعته، الضمانة دي مش ضمانة.

**الـ admission بيستخدم شرطين مش شرط واحد:**
```
admit iff  running_count < max_concurrency
      AND  sum(compute_weight) + candidate <= resource_capacity
```
رقم لعدد الشغلانات، ورقم تاني للمورد. رقم واحد ما ينفعش يعبر عن الاتنين.

---

## 10. أول أسبوع ليك

| اليوم | تعمل ايه |
|---|---|
| 1 | اقرا Part One من الـ Architecture Pack (sections 1-19) |
| 2 | افهم الـ input contract (section 4) والـ data products (section 5). دول أساس كل حاجة |
| 3 | اقرا الـ genericity proof (section 13). هتشوف بترول ومياه بنفس العقود بالظبط |
| 4 | اقرا الـ gates (sections 15 و 30.2 و 40.1). 46 gate. افهم ليه كل واحد موجود |
| 5 | راجع الـ measurement backlog. مفيش قرارات معمارية مفتوحة خالص |

### الـ measurement backlog: أرقام لازم تتقاس، مش قرارات معمارية

**مفيش قرار معماري مفتوح.** اللي باقي أرقام، وكل واحد منهم ليه مكان جاهز في الـ schema يتكتب فيه:

| الرقم | يتكتب فين |
|---|---|
| حدود الأهلية للـ encoder والـ supervised models | `model_details.acceptance_floor` والحدود الدنيا في Ch4 5.6.3 |
| قياس الهاردوير | الـ capacity model في Ch4 5.3.3 |
| مدة الاحتفاظ بالـ sequences والـ snapshots | `feature_snapshots.retention_until_utc` |
| نسبة السعة المحجوزة للـ interactive | حجز الـ `interactive` في Ch4 5.3.2 |

كلهم محتاجين **قياس**، مش نقاش تصميم.

---

### أسئلة تسألها ولازم تعرف إجابتها قبل ما تكتب سطر كود

1. الـ analytical grain عند العميل ده إيه؟
2. هل فيه genealogy؟ وقوتها إيه (none / sequential / transformational)؟
3. الـ outcome بيتسجل إمتى وفين؟ (ده اللي بيحدد الـ leakage)
4. أنهي بارامترات controllable وأنهي observed؟
5. الـ capability profile بيقول إيه؟ العميل ده على أي درجة؟

---

## 11. الحاجة الوحيدة اللي لازم تفهمها لو نسيت كل اللي فوق

**المحرك مسموح له يقول "مش عارف".**

فيه 6 حالات نهائية:

```
FINDING                  لقيت نتيجة
INSUFFICIENT_DATA        البيانات مش كفاية (مع الرقم اللي فشل)
NOT_APPLICABLE           مفيش method للحالة دي (مش عيب في البيانات)
REFUSED_BY_GUARD         فيه قاعدة منعتني
CONTRADICTED_BY_CONTROL  الـ negative control اتحرك، يبقى فيه غلط
MODEL_NOT_READY          الموديل لسه مش جاهز
```

منتج بيقول "مش عارف" بصراحة أقوى بكتير من منتج بيخترع رقم. والرفض ده هو الميزة، مش عيب.

---

*ملخص تعريفي، محدّث على Revision 7. المرجع الكامل والملزم هو `PPIQ_Layer_B_Architecture_Design_Pack.md`. ولو فيه اختلاف بينه وبين Chapters 2/3/4، الـ Chapters هي الصح.*

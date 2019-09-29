# Really useful - https://www.tensorflow.org/tutorials/load_data/images

print('######## Starting #######')

######################## Imports ########################
import numpy as np
import pathlib
import matplotlib.pyplot as plt
import matplotlib.image as mpimg
import GetData as d
import sys
import warnings

#sys.exit()

# Hide all 'FutureWarning'
with warnings.catch_warnings():  
    warnings.filterwarnings("ignore",category=FutureWarning)
    import tensorflow as tf
    from tensorflow import keras
AUTOTUNE = tf.data.experimental.AUTOTUNE
#########################################################

img_width = 45
img_height = 100
img_channels = 3
batch_size = 5

print("####### Arg1",sys.argv[1])
directory = sys.argv[1] #" C:\\Users\\Oliver Wickenden\\Documents\\TraderTools\\AutomatedTraderAI\\Models\\Trend"
outputs = sys.argv[2] # 6

savePath = directory + "\\model.h5"
(x_train, y_train), (x_test, y_test) = d.getRawDataSet(directory) # d.getJpegDataSet(directory)

#Test loading model
#loadedModel = keras.models.load_model(savePath)
#loadedModel.evaluate(x_test, y_test)
#sys.exit()

print("####### Running #######")

print("####### Got",len(x_train),"training inputs. Got",len(x_test),"test inputs")
# print("####### x_train",type(x_train),"y_train",type(y_train))
print("####### x_train[0] type",type(x_train[0]),"y_train[0] type",type(y_train[0]))
print("####### Data shape",x_train[0].shape)

# Setup model
# ----------------------
# Non convolutional
# ----------------------
model = tf.keras.models.Sequential([
  tf.keras.layers.Flatten(),
  tf.keras.layers.Dense(128, activation='relu'),
  tf.keras.layers.Dropout(0.2),
  tf.keras.layers.Dense(outputs, activation='softmax')
])

# ----------------------
# Convolutional
# ----------------------
#model = tf.keras.models.Sequential([
#  tf.keras.layers.Conv2D(32, kernel_size=(5, 5), strides=(1, 1), activation='relu',padding='same'),
#  tf.keras.layers.MaxPooling2D(pool_size=(2, 2), strides=(2, 2)),
#  tf.keras.layers.Conv2D(64, (5, 5), activation='relu'),
#  tf.keras.layers.MaxPooling2D(pool_size=(2, 2)),
#  tf.keras.layers.Flatten(),
#  tf.keras.layers.Dense(128, activation='relu'),
#  tf.keras.layers.Dense(5, activation='softmax')
#])

# Compile model
model.compile(optimizer='adam',
              loss='sparse_categorical_crossentropy',
              metrics=['accuracy'])

# Fit model
model.fit(x_train, y_train, epochs=1500) # batch_size=4)

model.summary()

print("####### Saving model to", savePath)
model.save(savePath)

# Evalulate results
print("####### Evaluating results")
model.evaluate(x_test, y_test)
print("####### Done")

# Try to load model
loadedModel = keras.models.load_model(savePath)
loadedModel.evaluate(x_test, y_test)